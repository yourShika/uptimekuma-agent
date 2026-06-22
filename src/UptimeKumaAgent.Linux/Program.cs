using System.Runtime.InteropServices;
using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Services;

namespace UptimeKumaAgent.Linux;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = CliOptions.Parse(args, out var error);
        if (!string.IsNullOrWhiteSpace(error))
        {
            Console.Error.WriteLine(error);
            PrintHelp();
            return 2;
        }

        if (options.ShowHelp || args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        if (options.ShowVersion)
        {
            Console.WriteLine(AppVersion.Current);
            return 0;
        }

        var paths = new LinuxAppPaths(options.ConfigPath);
        var configService = new ConfigService(paths);

        try
        {
            if (options.TestConfig)
            {
                var testConfig = configService.LoadOrThrow(createIfMissing: false);
                Console.WriteLine($"Konfiguration OK: {configService.ConfigPath}");
                Console.WriteLine($"Ping={testConfig.PingChecks.Count}, TCP={testConfig.TcpChecks.Count}, Dienste={testConfig.ServiceChecks.Count}, Laufwerke={testConfig.DriveChecks.Count}");
                return 0;
            }

            var logger = new Logger(paths);
            var config = configService.LoadOrThrow(createIfMissing: true);
            logger.SetLevel(config.Global.LogLevel);
            WarnAboutIgnoredWindowsSettings(config, logger);

            if (options.Once)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                var monitoring = CreateMonitoring(config, logger);
                var statuses = await monitoring.RunAllOnceAsync(sendPush: true, cts.Token).ConfigureAwait(false);
                foreach (var status in statuses)
                {
                    Console.WriteLine($"{status.Type}\t{status.Name}\t{status.State}\t{status.LastMessage}");
                }

                return 0;
            }

            if (options.Service)
            {
                await RunServiceAsync(configService, logger).ConfigureAwait(false);
                return 0;
            }

            PrintHelp();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Fehler: " + ex.Message);
            return 1;
        }
    }

    private static async Task RunServiceAsync(ConfigService configService, Logger logger)
    {
        var config = configService.LoadOrThrow(createIfMissing: true);
        logger.SetLevel(config.Global.LogLevel);
        var monitoring = CreateMonitoring(config, logger);
        using var stop = new CancellationTokenSource();
        using var reloadTimer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        var lastConfigWrite = GetConfigWriteTimeUtc(configService.ConfigPath);

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Cancel();
        };

        using var sigTerm = RegisterSignal(PosixSignal.SIGTERM, stop);
        using var sigInt = RegisterSignal(PosixSignal.SIGINT, stop);

        logger.Info("Linux-Dienst gestartet");
        if (config.Global.MonitoringAutoStart)
        {
            monitoring.Start();
        }
        else
        {
            logger.Warning("MonitoringAutoStart ist deaktiviert; Linux-Dienst wartet ohne aktive Checks");
        }

        try
        {
            while (!stop.IsCancellationRequested && await reloadTimer.WaitForNextTickAsync(stop.Token).ConfigureAwait(false))
            {
                var currentWrite = GetConfigWriteTimeUtc(configService.ConfigPath);
                if (currentWrite <= lastConfigWrite)
                {
                    continue;
                }

                var reloaded = configService.LoadOrThrow(createIfMissing: false);
                logger.SetLevel(reloaded.Global.LogLevel);
                WarnAboutIgnoredWindowsSettings(reloaded, logger);
                monitoring.ReloadConfig(reloaded);
                if (reloaded.Global.MonitoringAutoStart && !monitoring.IsRunning)
                {
                    monitoring.Start();
                }
                else if (!reloaded.Global.MonitoringAutoStart && monitoring.IsRunning)
                {
                    monitoring.Stop();
                }

                lastConfigWrite = currentWrite;
                logger.Info("Konfiguration im Linux-Dienst neu geladen");
            }
        }
        catch (OperationCanceledException)
        {
            // Normal service stop.
        }
        finally
        {
            monitoring.Stop();
            logger.Info("Linux-Dienst gestoppt");
        }
    }

    private static MonitoringService CreateMonitoring(AgentConfig config, Logger logger)
    {
        return new MonitoringService(
            config,
            logger,
            new KumaPushService(logger),
            new MachineInfoProvider(),
            new LinuxServiceManager(logger),
            new LinuxDriveMonitorService(logger),
            new LinuxTcpConnectionMonitor(logger),
            new LinuxPingService(logger));
    }

    private static IDisposable? RegisterSignal(PosixSignal signal, CancellationTokenSource stop)
    {
        try
        {
            return PosixSignalRegistration.Create(signal, context =>
            {
                context.Cancel = true;
                stop.Cancel();
            });
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }

    private static DateTime GetConfigWriteTimeUtc(string configPath)
    {
        return File.Exists(configPath) ? File.GetLastWriteTimeUtc(configPath) : DateTime.MinValue;
    }

    private static void WarnAboutIgnoredWindowsSettings(AgentConfig config, Logger logger)
    {
        if (config.Global.Autostart || config.Global.StartMinimized || config.Global.MinimizeToTrayOnClose || !string.IsNullOrWhiteSpace(config.Global.Theme))
        {
            logger.Warning("Linux-Headless ignoriert Windows-GUI-Einstellungen wie Theme, Tray, Autostart und StartMinimized.");
        }

        if (config.PingChecks.Cast<object>().Concat(config.TcpChecks).Concat(config.ServiceChecks).Any(check =>
            check switch
            {
                PingCheckConfig ping => ping.ForceKillRestartServicesOnTimeout,
                TcpCheckConfig tcp => tcp.ForceKillRestartServicesOnTimeout,
                ServiceCheckConfig service => service.ForceKillRestartServicesOnTimeout,
                _ => false
            }))
        {
            logger.Warning("Linux-Serviceaktionen ignorieren ForceKillRestartServicesOnTimeout; systemd steuert Stop-/Kill-Verhalten.");
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
        uptime-kuma-agent 1.0.8

        Optionen:
          --help             Hilfe anzeigen
          --version          Version anzeigen
          --config <path>    Konfigurationsdatei verwenden
          --test-config      Konfiguration laden und validieren
          --once             Alle aktivierten Checks einmal ausführen
          --service          Headless-Dienstmodus für systemd

        Standardpfade:
          Config:  /etc/uptime-kuma-agent/config.json
          Daten:   /var/lib/uptime-kuma-agent
          Logs:    /var/log/uptime-kuma-agent
          App:     /opt/uptime-kuma-agent
        """);
    }
}

internal sealed class CliOptions
{
    public bool ShowHelp { get; private init; }
    public bool ShowVersion { get; private init; }
    public string? ConfigPath { get; private init; }
    public bool TestConfig { get; private init; }
    public bool Once { get; private init; }
    public bool Service { get; private init; }

    public static CliOptions Parse(string[] args, out string error)
    {
        error = "";
        var options = new CliOptions();
        var mutable = new MutableOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                    mutable.ShowHelp = true;
                    break;
                case "--version":
                    mutable.ShowVersion = true;
                    break;
                case "--config":
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        error = "--config benötigt einen Pfad.";
                        return options;
                    }

                    mutable.ConfigPath = args[++i];
                    break;
                case "--test-config":
                    mutable.TestConfig = true;
                    break;
                case "--once":
                    mutable.Once = true;
                    break;
                case "--service":
                    mutable.Service = true;
                    break;
                default:
                    error = "Unbekanntes Argument: " + arg;
                    return options;
            }
        }

        var modes = new[] { mutable.TestConfig, mutable.Once, mutable.Service }.Count(value => value);
        if (modes > 1)
        {
            error = "--test-config, --once und --service dürfen nicht kombiniert werden.";
            return options;
        }

        return new CliOptions
        {
            ShowHelp = mutable.ShowHelp,
            ShowVersion = mutable.ShowVersion,
            ConfigPath = mutable.ConfigPath,
            TestConfig = mutable.TestConfig,
            Once = mutable.Once,
            Service = mutable.Service
        };
    }

    private sealed class MutableOptions
    {
        public bool ShowHelp { get; set; }
        public bool ShowVersion { get; set; }
        public string? ConfigPath { get; set; }
        public bool TestConfig { get; set; }
        public bool Once { get; set; }
        public bool Service { get; set; }
    }
}
