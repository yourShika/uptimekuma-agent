using System.Text.RegularExpressions;
using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Services;

namespace UptimeKumaAgent.Linux;

internal sealed partial class LinuxServiceManager : IServiceManager
{
    private readonly Logger _logger;

    public LinuxServiceManager(Logger logger)
    {
        _logger = logger;
    }

    public ServiceOperationResult GetStatus(string serviceName)
    {
        if (!LinuxServiceName.TryNormalize(serviceName, out var normalized, out var error))
        {
            return Failure("Dienstname ungültig", error);
        }

        var result = ProcessRunner
            .RunAsync("systemctl", new[] { "is-active", normalized }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        var output = FirstLine(result.StandardOutput);
        if (result.ExitCode == 0 && string.Equals(output, "active", StringComparison.OrdinalIgnoreCase))
        {
            return Success("Running", $"{normalized} ist active");
        }

        if (IsKnownInactiveState(output))
        {
            return Success(MapSystemdState(output), $"{normalized} ist {output}");
        }

        if (result.ExitCode == 127)
        {
            return Failure("systemctl fehlt", "systemctl wurde nicht gefunden. Linux-Serviceaktionen benötigen systemd.");
        }

        var message = result.CombinedOutput;
        if (LooksLikeMissingService(message, normalized) || string.Equals(output, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            return Failure("Dienst existiert nicht", $"systemd-Unit {normalized} existiert nicht");
        }

        return Failure("Dienstprüfung fehlgeschlagen", string.IsNullOrWhiteSpace(message) ? $"systemctl ExitCode={result.ExitCode}" : message);
    }

    public Task<ServiceOperationResult> StartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return RunActionAndWaitAsync("start", serviceName, "Running", timeout, cancellationToken);
    }

    public Task<ServiceOperationResult> StopServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken, bool forceKillOnTimeout = false)
    {
        return RunActionAndWaitAsync("stop", serviceName, "Stopped", timeout, cancellationToken);
    }

    public Task<ServiceOperationResult> RestartServiceAsync(
        string serviceName,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool forceKillOnStopTimeout = false)
    {
        if (forceKillOnStopTimeout)
        {
            _logger.Warning("Force-Kill bei Linux-Dienstneustart wird ignoriert; systemd KillMode/KillSignal steuert das Verhalten.");
        }

        return RunActionAndWaitAsync("restart", serviceName, "Running", timeout, cancellationToken);
    }

    private async Task<ServiceOperationResult> RunActionAndWaitAsync(
        string action,
        string serviceName,
        string desiredStatus,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!LinuxServiceName.TryNormalize(serviceName, out var normalized, out var error))
        {
            return Failure("Dienstname ungültig", error);
        }

        var result = await ProcessRunner
            .RunAsync("systemctl", new[] { action, normalized }, cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            return Failure(CategorizeSystemctlFailure(result.CombinedOutput, normalized), BuildActionError(action, normalized, result));
        }

        var deadline = DateTimeOffset.Now + timeout;
        while (DateTimeOffset.Now < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = GetStatus(normalized);
            if (status.Success && string.Equals(status.Status, desiredStatus, StringComparison.OrdinalIgnoreCase))
            {
                return Success(status.Status, $"systemctl {action} {normalized} erfolgreich");
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        return Failure($"Timeout bei systemctl {action}", $"Dienststatus wurde nicht {desiredStatus}", "");
    }

    private static string FirstLine(string text)
    {
        return text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
    }

    private static bool IsKnownInactiveState(string state)
    {
        return state is "inactive" or "failed" or "activating" or "deactivating" or "reloading";
    }

    private static string MapSystemdState(string state)
    {
        return state switch
        {
            "activating" or "reloading" => "StartPending",
            "deactivating" => "StopPending",
            _ => "Stopped"
        };
    }

    private static bool LooksLikeMissingService(string message, string normalized)
    {
        return message.Contains("not-found", StringComparison.OrdinalIgnoreCase)
               || message.Contains("could not be found", StringComparison.OrdinalIgnoreCase)
               || message.Contains($"Unit {normalized}", StringComparison.OrdinalIgnoreCase)
               || message.Contains("not loaded", StringComparison.OrdinalIgnoreCase);
    }

    private static string CategorizeSystemctlFailure(string message, string normalized)
    {
        if (message.Contains("Interactive authentication required", StringComparison.OrdinalIgnoreCase)
            || message.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("access denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("polkit", StringComparison.OrdinalIgnoreCase)
            || message.Contains("must be root", StringComparison.OrdinalIgnoreCase)
            || message.Contains("authentication is required", StringComparison.OrdinalIgnoreCase))
        {
            return "Root-/sudo-Rechte fehlen";
        }

        if (LooksLikeMissingService(message, normalized))
        {
            return "Dienst existiert nicht";
        }

        return "systemctl fehlgeschlagen";
    }

    private static string BuildActionError(string action, string normalized, ProcessResult result)
    {
        var message = result.CombinedOutput;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = $"systemctl {action} {normalized} ExitCode={result.ExitCode}";
        }

        if (CategorizeSystemctlFailure(message, normalized) == "Root-/sudo-Rechte fehlen")
        {
            return $"{message}. Bitte als root ausführen oder passende sudo-/systemd-Rechte konfigurieren.";
        }

        return message;
    }

    private static ServiceOperationResult Success(string status, string message)
    {
        return new ServiceOperationResult { Success = true, Status = status, Message = message };
    }

    private static ServiceOperationResult Failure(string category, string message, string status = "")
    {
        return new ServiceOperationResult { Success = false, ErrorCategory = category, Message = message, Status = status };
    }

}

internal static partial class LinuxServiceName
{
    public static bool TryNormalize(string? serviceName, out string normalized, out string error)
    {
        normalized = "";
        error = "";

        var value = serviceName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Dienstname fehlt.";
            return false;
        }

        if (value.Contains('/') || value.Contains('\\') || value.Contains("..", StringComparison.Ordinal) || value.Any(char.IsWhiteSpace))
        {
            error = "Dienstname darf keine Pfade, Leerzeichen oder '..' enthalten.";
            return false;
        }

        if (!ServiceNameRegex().IsMatch(value))
        {
            error = "Dienstname enthält ungültige Zeichen.";
            return false;
        }

        normalized = value.EndsWith(".service", StringComparison.OrdinalIgnoreCase)
            ? value
            : value + ".service";

        if (!normalized.EndsWith(".service", StringComparison.OrdinalIgnoreCase))
        {
            error = "Unter Linux werden nur systemd .service Units unterstützt.";
            normalized = "";
            return false;
        }

        return true;
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9@_.:-]*$")]
    private static partial Regex ServiceNameRegex();
}
