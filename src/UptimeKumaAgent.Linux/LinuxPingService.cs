using System.Diagnostics;
using System.Text.RegularExpressions;
using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Services;

namespace UptimeKumaAgent.Linux;

internal sealed partial class LinuxPingService : IPingService
{
    private readonly Logger _logger;
    private readonly DefaultPingService _inner = new();

    public LinuxPingService(Logger logger)
    {
        _logger = logger;
    }

    public async Task<MonitorCheckResult> PingAsync(PingRequest request, CancellationToken cancellationToken)
    {
        var result = await _inner.PingAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsUp || !LooksLikeIcmpPermissionIssue(result.Message))
        {
            return result;
        }

        _logger.Warning("ICMP-Ping über .NET fehlgeschlagen; versuche Linux-ping-Fallback. Ursache: " + result.Message);
        return await RunPingCommandAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<MonitorCheckResult> RunPingCommandAsync(PingRequest request, CancellationToken cancellationToken)
    {
        var timeoutSeconds = Math.Max(1, (int)Math.Ceiling(request.TimeoutMs / 1000.0));
        var stopwatch = Stopwatch.StartNew();
        var result = await ProcessRunner
            .RunAsync("ping", new[] { "-c", "1", "-W", timeoutSeconds.ToString(), request.Host }, cancellationToken)
            .ConfigureAwait(false);
        stopwatch.Stop();

        if (result.ExitCode == 0)
        {
            return MonitorCheckResult.Up($"Ping OK: {request.Name} erreichbar", ExtractPingMs(result.StandardOutput) ?? Math.Max(1, stopwatch.ElapsedMilliseconds));
        }

        if (result.ExitCode == 127)
        {
            return MonitorCheckResult.Down(
                $"Ping Fehler: {request.Name} ICMP-Rechte fehlen und das Linux-ping-Kommando wurde nicht gefunden",
                "Ping fehlgeschlagen",
                stopwatch.ElapsedMilliseconds);
        }

        var message = string.IsNullOrWhiteSpace(result.CombinedOutput)
            ? $"ping ExitCode={result.ExitCode}"
            : result.CombinedOutput;
        return MonitorCheckResult.Down($"Ping Fehler: {request.Name} {message}", "Ping fehlgeschlagen", stopwatch.ElapsedMilliseconds);
    }

    private static long? ExtractPingMs(string output)
    {
        var match = PingTimeRegex().Match(output);
        if (!match.Success)
        {
            return null;
        }

        return decimal.TryParse(match.Groups["ms"].Value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? Math.Max(1, (long)Math.Round(value))
            : null;
    }

    private static bool LooksLikeIcmpPermissionIssue(string message)
    {
        return message.Contains("Operation not permitted", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Access denied", StringComparison.OrdinalIgnoreCase)
               || message.Contains("raw socket", StringComparison.OrdinalIgnoreCase)
               || message.Contains("ICMP", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"time[=<](?<ms>[0-9]+(?:\.[0-9]+)?)\s*ms", RegexOptions.IgnoreCase)]
    private static partial Regex PingTimeRegex();
}
