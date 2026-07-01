using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Utils;
using AgentCheckState = UptimeKumaTrayAgent.Models.CheckState;

namespace UptimeKumaTrayAgent.Services;

public sealed class MonitoringService
{
    private readonly Logger _logger;
    private readonly KumaPushService _pushService;
    private readonly MachineInfoProvider _machineInfoProvider;
    private readonly IServiceManager _serviceManager;
    private readonly IDriveMonitorService _driveMonitorService;
    private readonly ITcpConnectionMonitor _tcpConnectionMonitor;
    private readonly IPingService _pingService;
    private readonly RuntimeStatusStore? _statusStore;
    private readonly string _snapshotSource;
    private readonly System.Threading.Timer? _persistTimer;
    private volatile bool _snapshotDirty;
    private readonly object _configLock = new();
    private readonly ConcurrentDictionary<string, CheckRuntimeStatus> _statuses = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nextRuns = new();
    private readonly ConcurrentDictionary<string, byte> _runningChecks = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastFailureServiceRestartAt = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _firstFailureAt = new();
    private readonly ConcurrentDictionary<string, long> _firstFailureUptimeMs = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _firstStoppedServiceAt = new();
    private readonly ConcurrentDictionary<string, long> _firstStoppedServiceUptimeMs = new();

    private AgentConfig _config;
    private CancellationTokenSource? _cancellation;
    private Task? _schedulerTask;
    private DateTimeOffset _nextWatchdogRun = DateTimeOffset.MinValue;

    public MonitoringService(
        AgentConfig config,
        Logger logger,
        KumaPushService pushService,
        MachineInfoProvider machineInfoProvider,
        IServiceManager serviceManager,
        IDriveMonitorService driveMonitorService,
        ITcpConnectionMonitor? tcpConnectionMonitor = null,
        IPingService? pingService = null,
        RuntimeStatusStore? statusStore = null,
        string snapshotSource = "")
    {
        _config = config;
        _logger = logger;
        _pushService = pushService;
        _machineInfoProvider = machineInfoProvider;
        _serviceManager = serviceManager;
        _driveMonitorService = driveMonitorService;
        _tcpConnectionMonitor = tcpConnectionMonitor ?? new NullTcpConnectionMonitor();
        _pingService = pingService ?? new DefaultPingService();
        _statusStore = statusStore;
        _snapshotSource = string.IsNullOrWhiteSpace(snapshotSource) ? "monitoring" : snapshotSource;
        EnsureStatuses(config);

        if (_statusStore is not null)
        {
            // Persist the live state on a fixed cadence rather than on every status mutation
            // (the scheduler touches every check once per second); readers poll ~1.5s.
            _persistTimer = new System.Threading.Timer(_ => FlushSnapshot(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }

    public event EventHandler? StateChanged;
    public event EventHandler? StatusesChanged;

    public bool IsRunning { get; private set; }
    public DateTimeOffset? LastRun { get; private set; }
    public DateTimeOffset? LastSuccessfulCheck { get; private set; }
    public string LastError { get; private set; } = "";

    public void ReloadConfig(AgentConfig config)
    {
        config.Normalize();
        lock (_configLock)
        {
            _config = config;
        }

        EnsureStatuses(config);
        _logger.SetLevel(config.Global.LogLevel);
        _logger.Info("Konfiguration in Monitoring-Service aktualisiert");
        OnStateChanged();
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cancellation = new CancellationTokenSource();
        IsRunning = true;
        _schedulerTask = Task.Run(() => SchedulerLoopAsync(_cancellation.Token));
        _logger.Info("Monitoring gestartet");
        OnStateChanged();
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        _cancellation?.Cancel();
        _logger.Info("Monitoring gestoppt");
        OnStateChanged();
        FlushSnapshot();
    }

    public IReadOnlyList<CheckRuntimeStatus> GetStatuses()
    {
        return _statuses.Values
            .Select(status => status.Clone())
            .OrderBy(status => status.Type)
            .ThenBy(status => status.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<CheckRuntimeStatus>> RunAllOnceAsync(bool sendPush, CancellationToken cancellationToken)
    {
        var config = GetConfigSnapshot();
        var checks = BuildScheduledChecks(config).Where(check => check.Enabled).ToArray();
        var tasks = checks.Select(check => RunScheduledCheckAsync(check, sendPush, cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
        return GetStatuses();
    }

    public async Task<CheckRuntimeStatus?> RunSingleAsync(CheckType type, string id, bool sendPush, CancellationToken cancellationToken)
    {
        var config = GetConfigSnapshot();
        var check = BuildScheduledChecks(config)
            .FirstOrDefault(item => item.Type == type && string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));

        if (check is null)
        {
            return null;
        }

        await RunScheduledCheckAsync(check, sendPush, cancellationToken).ConfigureAwait(false);
        return _statuses.TryGetValue(StatusKey(type, id), out var status) ? status.Clone() : null;
    }

    private async Task SchedulerLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var config = GetConfigSnapshot();
                EnsureStatuses(config);
                var now = DateTimeOffset.Now;

                foreach (var check in BuildScheduledChecks(config))
                {
                    var key = StatusKey(check.Type, check.Id);
                    if (!check.Enabled)
                    {
                        MarkDisabled(check);
                        continue;
                    }

                    var nextRun = _nextRuns.GetOrAdd(key, now);
                    if (nextRun > now)
                    {
                        UpdateNextRun(check, nextRun);
                        continue;
                    }

                    var scheduledNext = now.AddSeconds(check.IntervalSeconds);
                    _nextRuns[key] = scheduledNext;
                    UpdateNextRun(check, scheduledNext);

                    if (_runningChecks.TryAdd(key, 0))
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await RunScheduledCheckAsync(check, sendPush: true, cancellationToken).ConfigureAwait(false);
                            }
                            finally
                            {
                                _runningChecks.TryRemove(key, out _);
                            }
                        }, cancellationToken);
                    }
                }

                if (config.Watchdog.Enabled && now >= _nextWatchdogRun)
                {
                    _nextWatchdogRun = now.AddSeconds(config.Watchdog.IntervalSeconds);
                    _ = Task.Run(() => RunWatchdogAsync(config, cancellationToken), cancellationToken);
                }

                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal stop.
        }
        catch (Exception ex)
        {
            LastError = "Scheduler-Fehler: " + ex.Message;
            _logger.Error(LastError, ex);
            IsRunning = false;
            OnStateChanged();
        }
    }

    private async Task RunScheduledCheckAsync(ScheduledCheck check, bool sendPush, CancellationToken cancellationToken)
    {
        var key = StatusKey(check.Type, check.Id);
        SetStatus(check, status =>
        {
            status.IsRunning = true;
            status.Enabled = check.Enabled;
            status.State = AgentCheckState.Unknown;
        });

        MonitorCheckResult result;
        PushResult? pushResult = null;
        try
        {
            result = await ExecuteCheckAsync(check, cancellationToken).ConfigureAwait(false);
            if (result.IsUp)
            {
                ClearDeferredRestartState(key);
            }

            if (!result.IsUp)
            {
                result = await ApplyFailureServiceRestartsAsync(check, result, cancellationToken).ConfigureAwait(false);
            }

            LastRun = result.CheckedAt;

            if (result.IsUp)
            {
                LastSuccessfulCheck = result.CheckedAt;
            }
            else
            {
                LastError = result.ErrorCategory + ": " + result.Message;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (sendPush)
            {
                if (string.IsNullOrWhiteSpace(check.PushUrl))
                {
                    pushResult = new PushResult
                    {
                        Success = false,
                        ErrorCategory = "Push-URL fehlt",
                        Message = "Keine Uptime-Kuma-Push-URL konfiguriert"
                    };
                }
                else
                {
                    pushResult = await _pushService
                        .PushAsync(check.PushUrl, result, check.HttpTimeoutMs, cancellationToken)
                        .ConfigureAwait(false);

                    if (!pushResult.Success)
                    {
                        LastError = pushResult.ErrorCategory + ": " + pushResult.Message;
                    }
                }
            }

            SetStatus(check, status =>
            {
                status.IsRunning = false;
                status.LastRun = result.CheckedAt;
                status.LastResponseMs = result.PingMs;
                status.LastMessage = result.Message;
                status.State = ResolveState(result, pushResult, sendPush);
                status.LastError = BuildStatusError(result, pushResult, sendPush);
                if (result.IsUp)
                {
                    status.LastSuccessfulCheck = result.CheckedAt;
                }

                if (pushResult is not null)
                {
                    status.LastPushAt = pushResult.CompletedAt;
                    status.LastPushHttpStatus = pushResult.HttpStatusCode?.ToString() ?? pushResult.ErrorCategory;
                    if (pushResult.DurationMs.HasValue)
                    {
                        status.LastPushMs = pushResult.DurationMs;
                    }
                }
            });

            _logger.Info($"{check.Type} {check.Name}: {result.Message}; Push={pushResult?.Message ?? "nicht gesendet"}");
        }
        catch (OperationCanceledException)
        {
            SetStatus(check, status => status.IsRunning = false);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _logger.Error($"Check {check.Name} fehlgeschlagen", ex);
            SetStatus(check, status =>
            {
                status.IsRunning = false;
                status.LastRun = DateTimeOffset.Now;
                status.State = AgentCheckState.Down;
                status.LastError = "Check fehlgeschlagen: " + ex.Message;
                status.LastMessage = status.LastError;
            });
        }
        finally
        {
            OnStateChanged();
        }

        _ = key;
    }

    private async Task<MonitorCheckResult> ExecuteCheckAsync(ScheduledCheck check, CancellationToken cancellationToken)
    {
        return check.Type switch
        {
            CheckType.Ping => await ExecutePingAsync(check, cancellationToken).ConfigureAwait(false),
            CheckType.Tcp => await ExecuteTcpAsync(check, cancellationToken).ConfigureAwait(false),
            CheckType.Service => await ExecuteServiceAsync(check, cancellationToken).ConfigureAwait(false),
            CheckType.Drive => await ExecuteDriveAsync(check, cancellationToken).ConfigureAwait(false),
            _ => MonitorCheckResult.Down("Unbekannter Check-Typ", "Dienstprüfung fehlgeschlagen")
        };
    }

    private async Task<MonitorCheckResult> ApplyFailureServiceRestartsAsync(
        ScheduledCheck check,
        MonitorCheckResult result,
        CancellationToken cancellationToken)
    {
        if (check.RestartServicesOnFailure.Count == 0)
        {
            return result;
        }

        var key = StatusKey(check.Type, check.Id);
        var delayed = BuildDelayedRestartResult(
            key + ":failure-actions",
            _firstFailureAt,
            _firstFailureUptimeMs,
            check.RestartServicesDelayAfterBootMinutes,
            check.RestartServicesDelayAfterFailureMinutes,
            result.Message,
            result.ErrorCategory,
            result.PingMs);
        if (delayed is not null)
        {
            return delayed;
        }

        var now = DateTimeOffset.Now;
        if (_lastFailureServiceRestartAt.TryGetValue(key, out var lastRestartAt)
            && now - lastRestartAt < TimeSpan.FromSeconds(check.RestartServicesCooldownSeconds))
        {
            var remaining = TimeSpan.FromSeconds(check.RestartServicesCooldownSeconds) - (now - lastRestartAt);
            return MonitorCheckResult.Down(
                $"{result.Message} | Dienst-Neustart übersprungen: Cooldown noch {TimeFormatter.FormatDuration(remaining)}",
                result.ErrorCategory,
                result.PingMs);
        }

        _lastFailureServiceRestartAt[key] = now;
        var restartMessages = new List<string>();
        foreach (var serviceName in check.RestartServicesOnFailure)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var restart = await _serviceManager
                    .RestartServiceAsync(
                        serviceName,
                        TimeSpan.FromSeconds(45),
                        cancellationToken,
                        check.ForceKillRestartServicesOnTimeout)
                    .ConfigureAwait(false);

                if (restart.Success)
                {
                    restartMessages.Add($"{serviceName}=OK");
                    var forceText = check.ForceKillRestartServicesOnTimeout ? " mit Force-Kill bei Stop-Timeout" : "";
                    _logger.Warning($"{check.Type} {check.Name}: Fehleraktion hat Dienst {serviceName}{forceText} neu gestartet");
                }
                else
                {
                    restartMessages.Add($"{serviceName}=Fehler {restart.ErrorCategory}");
                    _logger.Warning($"{check.Type} {check.Name}: Fehleraktion für Dienst {serviceName} fehlgeschlagen: {restart.ErrorCategory} {restart.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                restartMessages.Add($"{serviceName}=Fehler {ex.Message}");
                _logger.Error($"{check.Type} {check.Name}: Fehleraktion für Dienst {serviceName} fehlgeschlagen", ex);
            }
        }

        return MonitorCheckResult.Down(
            $"{result.Message} | Dienst-Neustart: {string.Join("; ", restartMessages)}",
            result.ErrorCategory,
            result.PingMs);
    }

    private async Task<MonitorCheckResult> ExecutePingAsync(ScheduledCheck check, CancellationToken cancellationToken)
    {
        if (!ValidationUtils.IsValidHost(check.Host))
        {
            return MonitorCheckResult.Down($"Ping Fehler: {check.Target} Hostname ungültig", "Hostname ungültig");
        }

        var result = await _pingService
            .PingAsync(new PingRequest(check.Host, check.Name, check.TimeoutMs), cancellationToken)
            .ConfigureAwait(false);

        return result.IsUp
            ? MonitorCheckResult.Up(AppendMachineInfo(check, result.Message), result.PingMs)
            : MonitorCheckResult.Down(BuildDownSinceMessage(check, result.Message), result.ErrorCategory, result.PingMs);
    }

    private async Task<MonitorCheckResult> ExecuteTcpAsync(ScheduledCheck check, CancellationToken cancellationToken)
    {
        if (!ValidationUtils.IsValidHost(check.Host))
        {
            return MonitorCheckResult.Down($"TCP Fehler: {check.Target} Hostname ungültig", "Hostname ungültig");
        }

        if (!ValidationUtils.IsValidPort(check.Port))
        {
            return MonitorCheckResult.Down($"TCP Fehler: {check.Target} Port ungültig", "TCP-Port nicht erreichbar");
        }

        LogTcpConnections(check);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(check.TimeoutMs));
        using var client = new TcpClient();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await client.ConnectAsync(check.Host, check.Port, timeout.Token).ConfigureAwait(false);
            stopwatch.Stop();
            return MonitorCheckResult.Up(
                AppendMachineInfo(check, $"TCP OK: {check.Host}:{check.Port} erreichbar"),
                Math.Max(1, stopwatch.ElapsedMilliseconds));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return MonitorCheckResult.Down($"TCP Fehler: {check.Host}:{check.Port} Timeout", "TCP-Timeout", stopwatch.ElapsedMilliseconds);
        }
        catch (SocketException ex)
        {
            stopwatch.Stop();
            return MonitorCheckResult.Down(
                $"TCP Fehler: {check.Host}:{check.Port} nicht erreichbar oder Timeout",
                MapSocketErrorForTarget(ex),
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            return MonitorCheckResult.Down(
                $"TCP Fehler: {check.Host}:{check.Port} {ex.Message}",
                "TCP-Port nicht erreichbar",
                stopwatch.ElapsedMilliseconds);
        }
    }

    private void LogTcpConnections(ScheduledCheck check)
    {
        if (!check.LogTcpConnections)
        {
            return;
        }

        try
        {
            var connections = _tcpConnectionMonitor.GetConnections(check.Host, check.Port, check.TcpConnectionLogDirection);
            var direction = TcpConnectionLogDirections.Normalize(check.TcpConnectionLogDirection);
            if (connections.Count == 0)
            {
                _logger.Info($"TCP-Verbindungen {check.Name} ({check.Host}:{check.Port}, {direction}): keine passenden Verbindungen gefunden");
                return;
            }

            foreach (var connection in connections)
            {
                _logger.Info($"TCP-Verbindung {check.Name} ({check.Host}:{check.Port}, {direction}): {connection.ToLogText()}");
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"TCP-Verbindungen {check.Name} ({check.Host}:{check.Port}) konnten nicht gelesen werden: {ex.Message}");
        }
    }

    private async Task<MonitorCheckResult> ExecuteServiceAsync(ScheduledCheck check, CancellationToken cancellationToken)
    {
        var statusResult = _serviceManager.GetStatus(check.ServiceName);
        if (!statusResult.Success)
        {
            return MonitorCheckResult.Down(
                $"Dienst Fehler: {check.ServiceDisplayName} {statusResult.Message}",
                statusResult.ErrorCategory);
        }

        if (string.Equals(statusResult.Status, check.ExpectedStatus, StringComparison.OrdinalIgnoreCase))
        {
            return MonitorCheckResult.Up(AppendMachineInfo(check, $"Dienst OK: {check.ServiceDisplayName} ist {statusResult.Status}"));
        }

        if (check.RestartIfStopped
            && string.Equals(check.ExpectedStatus, "Running", StringComparison.OrdinalIgnoreCase)
            && string.Equals(statusResult.Status, "Stopped", StringComparison.OrdinalIgnoreCase))
        {
            var delayResult = BuildDelayedRestartResult(
                StatusKey(check.Type, check.Id) + ":restart-if-stopped",
                _firstStoppedServiceAt,
                _firstStoppedServiceUptimeMs,
                check.RestartIfStoppedDelayAfterBootMinutes,
                check.RestartIfStoppedDelayAfterFailureMinutes,
                $"Dienst {check.ServiceDisplayName} ist gestoppt. Automatischer Neustart wartet.",
                "Dienststatus entspricht nicht dem erwarteten Status",
                null);
            if (delayResult is not null)
            {
                return delayResult;
            }

            var restart = await _serviceManager
                .StartServiceAsync(check.ServiceName, TimeSpan.FromSeconds(30), cancellationToken)
                .ConfigureAwait(false);

            var restartMessage = restart.Success
                ? "Neustart wurde versucht"
                : $"Neustart fehlgeschlagen: {restart.ErrorCategory}";

            return MonitorCheckResult.Down(
                AppendMachineInfo(check, $"Dienst {check.ServiceDisplayName} war gestoppt. {restartMessage}."),
                restart.Success ? "Dienststatus entspricht nicht dem erwarteten Status" : restart.ErrorCategory);
        }

        return MonitorCheckResult.Down(
            $"Dienst Fehler: {check.ServiceDisplayName} ist {statusResult.Status}, erwartet {check.ExpectedStatus}",
            "Dienststatus entspricht nicht dem erwarteten Status");
    }

    private async Task<MonitorCheckResult> ExecuteDriveAsync(ScheduledCheck check, CancellationToken cancellationToken)
    {
        var driveConfig = new DriveCheckConfig
        {
            Name = check.Name,
            Path = check.DrivePath,
            IntervalSeconds = check.IntervalSeconds,
            MinimumFreePercent = check.MinimumFreePercent,
            MinimumFreeGb = check.MinimumFreeGb,
            ReconnectIfUnavailable = check.ReconnectIfUnavailable,
            ReconnectPath = check.ReconnectPath,
            LogDetails = check.LogDriveDetails,
            SendMachineInfo = check.SendMachineInfo
        };

        var drive = await _driveMonitorService.CheckAsync(driveConfig, cancellationToken).ConfigureAwait(false);
        if (check.LogDriveDetails)
        {
            _logger.Info($"Laufwerk {check.Name}: {drive.BuildDetailText()}");
        }

        var reconnectText = drive.ReconnectAttempted
            ? $" | Reconnect={(drive.ReconnectSuccess ? "OK" : "Fehler")}: {drive.ReconnectMessage}"
            : "";

        if (drive.IsReady)
        {
            return MonitorCheckResult.Up(
                AppendMachineInfo(check, $"Laufwerk OK: {check.Name} {drive.Message}{reconnectText}"));
        }

        return MonitorCheckResult.Down(
            $"Laufwerk Fehler: {check.Name} {drive.Message}{reconnectText}",
            drive.Category);
    }

    private async Task RunWatchdogAsync(AgentConfig config, CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTimeOffset.Now;
            var lastSuccessAge = LastSuccessfulCheck is null ? TimeSpan.MaxValue : now - LastSuccessfulCheck.Value;
            var activeChecks = _runningChecks.Count;
            var maxWithoutSuccess = TimeSpan.FromSeconds(config.Watchdog.MaxSecondsWithoutSuccessfulCheck);

            MonitorCheckResult result;
            if (LastSuccessfulCheck is null)
            {
                result = MonitorCheckResult.Down("Agent läuft. Noch kein erfolgreicher Check.", "Watchdog: kein erfolgreicher Check");
            }
            else if (lastSuccessAge > maxWithoutSuccess)
            {
                result = MonitorCheckResult.Down(
                    $"Agent läuft. Letzter erfolgreicher Check vor {TimeFormatter.FormatDuration(lastSuccessAge)}. Aktive Checks={activeChecks}",
                    "Watchdog: zu lange kein erfolgreicher Check");
            }
            else
            {
                var message = $"Agent läuft. Letzter Check vor {TimeFormatter.FormatAge(LastSuccessfulCheck)}. Aktive Checks={activeChecks}";
                if (config.Global.SendMachineInfo && config.Watchdog.SendMachineInfo)
                {
                    message += " | " + _machineInfoProvider.BuildSummary();
                }

                result = MonitorCheckResult.Up(message);
            }

            if (!string.IsNullOrWhiteSpace(config.Watchdog.PushUrl))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var push = await _pushService
                    .PushAsync(config.Watchdog.PushUrl, result, config.Global.HttpTimeoutMs, cancellationToken)
                    .ConfigureAwait(false);

                _logger.Info($"Watchdog: {result.Message}; Push={push.Message}");
                if (!push.Success)
                {
                    LastError = push.ErrorCategory + ": " + push.Message;
                }
            }
            else
            {
                _logger.Debug("Watchdog aktiv, aber keine Push-URL konfiguriert");
            }
        }
        catch (OperationCanceledException)
        {
            // Normal stop.
        }
        catch (Exception ex)
        {
            LastError = "Watchdog-Fehler: " + ex.Message;
            _logger.Error(LastError, ex);
            OnStateChanged();
        }
    }

    private static AgentCheckState ResolveState(MonitorCheckResult result, PushResult? pushResult, bool sendPush)
    {
        if (!result.IsUp)
        {
            return AgentCheckState.Down;
        }

        if (sendPush && pushResult is { Success: false })
        {
            return AgentCheckState.Warning;
        }

        return AgentCheckState.Up;
    }

    private static string BuildStatusError(MonitorCheckResult result, PushResult? pushResult, bool sendPush)
    {
        if (!result.IsUp)
        {
            return string.IsNullOrWhiteSpace(result.ErrorCategory) ? result.Message : result.ErrorCategory + ": " + result.Message;
        }

        if (sendPush && pushResult is { Success: false })
        {
            return pushResult.ErrorCategory + ": " + pushResult.Message;
        }

        return "";
    }

    private string AppendMachineInfo(ScheduledCheck check, string message)
    {
        return check.SendMachineInfo && check.GlobalSendMachineInfo
            ? message + " | " + _machineInfoProvider.BuildSummary()
            : message;
    }

    private string BuildDownSinceMessage(ScheduledCheck check, string baseMessage)
    {
        var key = StatusKey(check.Type, check.Id);
        if (_statuses.TryGetValue(key, out var status) && status.LastSuccessfulCheck is not null)
        {
            var age = DateTimeOffset.Now - status.LastSuccessfulCheck.Value;
            return $"{baseMessage} seit {TimeFormatter.FormatDuration(age)}";
        }

        return baseMessage;
    }

    private void ClearDeferredRestartState(string statusKey)
    {
        RemoveDeferredRestartState(statusKey + ":failure-actions", _firstFailureAt, _firstFailureUptimeMs);
        RemoveDeferredRestartState(statusKey + ":restart-if-stopped", _firstStoppedServiceAt, _firstStoppedServiceUptimeMs);
    }

    private static void RemoveDeferredRestartState(
        string key,
        ConcurrentDictionary<string, DateTimeOffset> firstSeen,
        ConcurrentDictionary<string, long> firstUptimeMs)
    {
        firstSeen.TryRemove(key, out _);
        firstUptimeMs.TryRemove(key, out _);
    }

    private static MonitorCheckResult? BuildDelayedRestartResult(
        string key,
        ConcurrentDictionary<string, DateTimeOffset> firstSeen,
        ConcurrentDictionary<string, long> firstUptimeMs,
        int delayAfterBootMinutes,
        int delayAfterFailureMinutes,
        string baseMessage,
        string category,
        long? pingMs)
    {
        var now = DateTimeOffset.Now;
        var currentUptimeMs = Math.Max(0, Environment.TickCount64);
        var first = firstSeen.GetOrAdd(key, now);
        var firstUptime = firstUptimeMs.GetOrAdd(key, currentUptimeMs);
        var bootDelay = TimeSpan.FromMinutes(Math.Clamp(delayAfterBootMinutes, 0, 1440));
        var failureDelay = TimeSpan.FromMinutes(Math.Clamp(delayAfterFailureMinutes, 0, 1440));

        if (bootDelay > TimeSpan.Zero)
        {
            var uptime = TimeSpan.FromMilliseconds(currentUptimeMs);
            if (uptime < bootDelay)
            {
                return MonitorCheckResult.Down(
                    $"{baseMessage} | Dienst-Neustart wartet nach Systemstart noch {TimeFormatter.FormatDuration(bootDelay - uptime)}",
                    category,
                    pingMs);
            }
        }

        var firstSeenDuringBootDelay = bootDelay > TimeSpan.Zero && TimeSpan.FromMilliseconds(firstUptime) < bootDelay;
        if (failureDelay > TimeSpan.Zero && !firstSeenDuringBootDelay)
        {
            var age = now - first;
            if (age < failureDelay)
            {
                return MonitorCheckResult.Down(
                    $"{baseMessage} | Dienst-Neustart wartet nach Fehlerkennung noch {TimeFormatter.FormatDuration(failureDelay - age)}",
                    category,
                    pingMs);
            }
        }

        return null;
    }

    private void EnsureStatuses(AgentConfig config)
    {
        var knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var check in BuildScheduledChecks(config))
        {
            var key = StatusKey(check.Type, check.Id);
            knownKeys.Add(key);
            _statuses.AddOrUpdate(
                key,
                _ => CreateStatus(check),
                (_, existing) =>
                {
                    existing.Name = check.Name;
                    existing.Target = check.Target;
                    existing.Enabled = check.Enabled;
                    if (!check.Enabled)
                    {
                        existing.State = AgentCheckState.Disabled;
                        existing.NextRun = null;
                        existing.IsRunning = false;
                    }

                    return existing;
                });
        }

        foreach (var key in _statuses.Keys)
        {
            if (!knownKeys.Contains(key))
            {
                _statuses.TryRemove(key, out _);
                _nextRuns.TryRemove(key, out _);
            }
        }

        OnStatusesChanged();
    }

    private void MarkDisabled(ScheduledCheck check)
    {
        SetStatus(check, status =>
        {
            status.Enabled = false;
            status.State = AgentCheckState.Disabled;
            status.NextRun = null;
            status.IsRunning = false;
        });
    }

    private void UpdateNextRun(ScheduledCheck check, DateTimeOffset nextRun)
    {
        SetStatus(check, status =>
        {
            status.Enabled = true;
            status.NextRun = nextRun;
        });
    }

    private void SetStatus(ScheduledCheck check, Action<CheckRuntimeStatus> update)
    {
        var key = StatusKey(check.Type, check.Id);
        _statuses.AddOrUpdate(
            key,
            _ =>
            {
                var status = CreateStatus(check);
                update(status);
                return status;
            },
            (_, status) =>
            {
                status.Name = check.Name;
                status.Target = check.Target;
                status.Enabled = check.Enabled;
                update(status);
                return status;
            });
        OnStatusesChanged();
    }

    private static CheckRuntimeStatus CreateStatus(ScheduledCheck check)
    {
        return new CheckRuntimeStatus
        {
            Id = check.Id,
            Type = check.Type,
            Name = check.Name,
            Target = check.Target,
            Enabled = check.Enabled,
            State = check.Enabled ? AgentCheckState.Unknown : AgentCheckState.Disabled
        };
    }

    private AgentConfig GetConfigSnapshot()
    {
        lock (_configLock)
        {
            return _config;
        }
    }

    private static IReadOnlyList<ScheduledCheck> BuildScheduledChecks(AgentConfig config)
    {
        var checks = new List<ScheduledCheck>();

        checks.AddRange(config.PingChecks.Select(check => new ScheduledCheck
        {
            Type = CheckType.Ping,
            Id = check.Id,
            Enabled = check.Enabled,
            Name = string.IsNullOrWhiteSpace(check.Name) ? check.Host : check.Name,
            Target = check.Host,
            Host = check.Host,
            PushUrl = check.PushUrl,
            IntervalSeconds = check.IntervalSeconds,
            TimeoutMs = check.TimeoutMs,
            HttpTimeoutMs = config.Global.HttpTimeoutMs,
            SendMachineInfo = check.SendMachineInfo,
            GlobalSendMachineInfo = config.Global.SendMachineInfo,
            RestartServicesOnFailure = check.RestartServicesOnFailure,
            RestartServicesCooldownSeconds = check.RestartServicesCooldownSeconds,
            RestartServicesDelayAfterBootMinutes = check.RestartServicesDelayAfterBootMinutes,
            RestartServicesDelayAfterFailureMinutes = check.RestartServicesDelayAfterFailureMinutes,
            ForceKillRestartServicesOnTimeout = check.ForceKillRestartServicesOnTimeout
        }));

        checks.AddRange(config.TcpChecks.Select(check => new ScheduledCheck
        {
            Type = CheckType.Tcp,
            Id = check.Id,
            Enabled = check.Enabled,
            Name = string.IsNullOrWhiteSpace(check.Name) ? $"{check.Host}:{check.Port}" : check.Name,
            Target = $"{check.Host}:{check.Port}",
            Host = check.Host,
            Port = check.Port,
            PushUrl = check.PushUrl,
            IntervalSeconds = check.IntervalSeconds,
            TimeoutMs = check.TimeoutMs,
            HttpTimeoutMs = config.Global.HttpTimeoutMs,
            SendMachineInfo = check.SendMachineInfo,
            GlobalSendMachineInfo = config.Global.SendMachineInfo,
            RestartServicesOnFailure = check.RestartServicesOnFailure,
            RestartServicesCooldownSeconds = check.RestartServicesCooldownSeconds,
            RestartServicesDelayAfterBootMinutes = check.RestartServicesDelayAfterBootMinutes,
            RestartServicesDelayAfterFailureMinutes = check.RestartServicesDelayAfterFailureMinutes,
            ForceKillRestartServicesOnTimeout = check.ForceKillRestartServicesOnTimeout,
            LogTcpConnections = check.LogTcpConnections,
            TcpConnectionLogDirection = check.TcpConnectionLogDirection
        }));

        checks.AddRange(config.ServiceChecks.Select(check => new ScheduledCheck
        {
            Type = CheckType.Service,
            Id = check.Id,
            Enabled = check.Enabled,
            Name = string.IsNullOrWhiteSpace(check.DisplayName) ? check.ServiceName : check.DisplayName,
            Target = check.ServiceName,
            ServiceName = check.ServiceName,
            ServiceDisplayName = string.IsNullOrWhiteSpace(check.DisplayName) ? check.ServiceName : check.DisplayName,
            ExpectedStatus = check.ExpectedStatus,
            RestartIfStopped = check.RestartIfStopped,
            PushUrl = check.PushUrl,
            IntervalSeconds = check.IntervalSeconds,
            TimeoutMs = 30000,
            HttpTimeoutMs = config.Global.HttpTimeoutMs,
            SendMachineInfo = check.SendMachineInfo,
            GlobalSendMachineInfo = config.Global.SendMachineInfo,
            RestartServicesOnFailure = check.RestartServicesOnFailure,
            RestartServicesCooldownSeconds = check.RestartServicesCooldownSeconds,
            RestartServicesDelayAfterBootMinutes = check.RestartServicesDelayAfterBootMinutes,
            RestartServicesDelayAfterFailureMinutes = check.RestartServicesDelayAfterFailureMinutes,
            ForceKillRestartServicesOnTimeout = check.ForceKillRestartServicesOnTimeout,
            RestartIfStoppedDelayAfterBootMinutes = check.RestartIfStoppedDelayAfterBootMinutes,
            RestartIfStoppedDelayAfterFailureMinutes = check.RestartIfStoppedDelayAfterFailureMinutes
        }));

        checks.AddRange(config.DriveChecks.Select(check => new ScheduledCheck
        {
            Type = CheckType.Drive,
            Id = check.Id,
            Enabled = check.Enabled,
            Name = string.IsNullOrWhiteSpace(check.Name) ? check.Path : check.Name,
            Target = check.Path,
            DrivePath = check.Path,
            PushUrl = check.PushUrl,
            IntervalSeconds = check.IntervalSeconds,
            TimeoutMs = 30000,
            HttpTimeoutMs = config.Global.HttpTimeoutMs,
            SendMachineInfo = check.SendMachineInfo,
            GlobalSendMachineInfo = config.Global.SendMachineInfo,
            MinimumFreePercent = check.MinimumFreePercent,
            MinimumFreeGb = check.MinimumFreeGb,
            ReconnectIfUnavailable = check.ReconnectIfUnavailable,
            ReconnectPath = check.ReconnectPath,
            LogDriveDetails = check.LogDetails
        }));

        return checks;
    }

    private static string MapSocketErrorForTarget(SocketException exception)
    {
        return exception.SocketErrorCode switch
        {
            SocketError.HostNotFound or SocketError.NoData => "DNS-Problem",
            SocketError.TimedOut => "TCP-Timeout",
            SocketError.ConnectionRefused => "TCP-Port nicht erreichbar",
            SocketError.NetworkUnreachable or SocketError.HostUnreachable => "Zielsystem offline",
            _ => "TCP-Port nicht erreichbar"
        };
    }

    private static string StatusKey(CheckType type, string id)
    {
        return $"{type}:{id}";
    }

    private void OnStateChanged()
    {
        _snapshotDirty = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnStatusesChanged()
    {
        _snapshotDirty = true;
        StatusesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FlushSnapshot()
    {
        if (_statusStore is null || !_snapshotDirty)
        {
            return;
        }

        _snapshotDirty = false;
        try
        {
            _statusStore.Write(new RuntimeStatusSnapshot
            {
                UpdatedAt = DateTimeOffset.Now,
                ProcessId = Environment.ProcessId,
                Source = _snapshotSource,
                IsRunning = IsRunning,
                LastRun = LastRun,
                LastSuccessfulCheck = LastSuccessfulCheck,
                LastError = LastError,
                Statuses = _statuses.Values.Select(status => status.Clone()).ToList()
            });
        }
        catch
        {
            // Persisting the snapshot is best-effort; ignore transient IO failures.
        }
    }

    private sealed class ScheduledCheck
    {
        public CheckType Type { get; init; }
        public string Id { get; init; } = "";
        public bool Enabled { get; init; }
        public string Name { get; init; } = "";
        public string Target { get; init; } = "";
        public string Host { get; init; } = "";
        public int Port { get; init; }
        public string ServiceName { get; init; } = "";
        public string ServiceDisplayName { get; init; } = "";
        public string DrivePath { get; init; } = "";
        public string ExpectedStatus { get; init; } = "Running";
        public bool RestartIfStopped { get; init; }
        public string PushUrl { get; init; } = "";
        public int IntervalSeconds { get; init; }
        public int TimeoutMs { get; init; }
        public int HttpTimeoutMs { get; init; }
        public bool SendMachineInfo { get; init; }
        public bool GlobalSendMachineInfo { get; init; }
        public IReadOnlyList<string> RestartServicesOnFailure { get; init; } = Array.Empty<string>();
        public int RestartServicesCooldownSeconds { get; init; } = 300;
        public int RestartServicesDelayAfterBootMinutes { get; init; }
        public int RestartServicesDelayAfterFailureMinutes { get; init; }
        public bool ForceKillRestartServicesOnTimeout { get; init; }
        public int RestartIfStoppedDelayAfterBootMinutes { get; init; }
        public int RestartIfStoppedDelayAfterFailureMinutes { get; init; }
        public bool LogTcpConnections { get; init; }
        public string TcpConnectionLogDirection { get; init; } = TcpConnectionLogDirections.Both;
        public int MinimumFreePercent { get; init; }
        public decimal MinimumFreeGb { get; init; }
        public bool ReconnectIfUnavailable { get; init; }
        public string ReconnectPath { get; init; } = "";
        public bool LogDriveDetails { get; init; }
    }
}
