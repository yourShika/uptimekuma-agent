using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Services;

namespace UptimeKumaAgent.Linux;

internal sealed class LinuxDriveMonitorService : IDriveMonitorService
{
    private readonly Logger _logger;

    public LinuxDriveMonitorService(Logger logger)
    {
        _logger = logger;
    }

    public async Task<DriveCheckResult> CheckAsync(DriveCheckConfig check, CancellationToken cancellationToken)
    {
        var result = CheckCore(check);
        if (!result.IsReady && check.ReconnectIfUnavailable)
        {
            var reconnect = await TryReconnectAsync(check, cancellationToken).ConfigureAwait(false);
            var afterReconnect = CheckCore(check);
            return afterReconnect with
            {
                ReconnectAttempted = true,
                ReconnectSuccess = reconnect.Success && afterReconnect.IsReady,
                ReconnectMessage = reconnect.Message
            };
        }

        return result;
    }

    private static DriveCheckResult CheckCore(DriveCheckConfig check)
    {
        var path = NormalizeLinuxPath(check.Path);
        if (string.IsNullOrWhiteSpace(path))
        {
            return DriveCheckResult.Down("", "Laufwerkspfad fehlt", "Mountpoint fehlt");
        }

        if (LooksLikeWindowsPath(path))
        {
            return DriveCheckResult.Down(path, "Windows-Pfad unter Linux nicht unterstützt", "UNC-Pfade und Laufwerksbuchstaben werden unter Linux nicht verwendet");
        }

        if (!Directory.Exists(path))
        {
            return DriveCheckResult.Down(path, "Mountpoint nicht erreichbar", $"{path} existiert nicht oder ist nicht erreichbar");
        }

        try
        {
            var drive = FindBestDrive(path);
            if (drive is null)
            {
                return DriveCheckResult.Down(path, "Mountpoint nicht gefunden", $"{path} wurde nicht in den gemeldeten Dateisystemen gefunden");
            }

            if (!drive.IsReady)
            {
                return DriveCheckResult.Down(path, "Mountpoint nicht bereit", $"{path} ist nicht bereit");
            }

            var freePercent = drive.TotalSize > 0
                ? (int)Math.Round((double)drive.AvailableFreeSpace / drive.TotalSize * 100)
                : 0;
            var freeGb = BytesToGb(drive.AvailableFreeSpace);
            var displayPath = drive.Name.TrimEnd('/') is "" ? "/" : drive.Name.TrimEnd('/');

            if (check.MinimumFreePercent > 0 && freePercent < check.MinimumFreePercent)
            {
                return DriveCheckResult.Down(
                    displayPath,
                    "Zu wenig freier Speicher",
                    $"{displayPath} hat nur {freePercent}% frei, erwartet mindestens {check.MinimumFreePercent}%",
                    drive.TotalSize,
                    drive.AvailableFreeSpace,
                    freePercent,
                    drive.DriveFormat,
                    drive.DriveType.ToString());
            }

            if (check.MinimumFreeGb > 0 && freeGb < check.MinimumFreeGb)
            {
                return DriveCheckResult.Down(
                    displayPath,
                    "Zu wenig freier Speicher",
                    $"{displayPath} hat nur {freeGb:N1} GB frei, erwartet mindestens {check.MinimumFreeGb:N1} GB",
                    drive.TotalSize,
                    drive.AvailableFreeSpace,
                    freePercent,
                    drive.DriveFormat,
                    drive.DriveType.ToString());
            }

            return DriveCheckResult.Up(
                displayPath,
                drive.DriveType.ToString(),
                drive.DriveFormat,
                drive.TotalSize,
                drive.AvailableFreeSpace,
                freePercent,
                $"{displayPath} bereit, {freePercent}% frei ({freeGb:N1} GB)");
        }
        catch (Exception ex)
        {
            return DriveCheckResult.Down(path, "Laufwerksprüfung fehlgeschlagen", ex.Message);
        }
    }

    private async Task<ServiceOperationResult> TryReconnectAsync(DriveCheckConfig check, CancellationToken cancellationToken)
    {
        var mountPoint = NormalizeLinuxPath(check.Path);
        if (string.IsNullOrWhiteSpace(mountPoint) || LooksLikeWindowsPath(mountPoint))
        {
            return Failure("Mount-Reconnect nicht erlaubt", "Reconnect ist unter Linux nur für absolute Mountpoints erlaubt");
        }

        if (!FstabContainsMountPoint(mountPoint))
        {
            return Failure("Mount-Reconnect nicht erlaubt", $"{mountPoint} steht nicht in /etc/fstab; freies Mounten mit Benutzereingaben ist deaktiviert");
        }

        var result = await ProcessRunner.RunAsync("mount", new[] { mountPoint }, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 0)
        {
            _logger.Info($"Mount-Reconnect {mountPoint}: OK");
            return Success($"mount {mountPoint} erfolgreich");
        }

        var message = string.IsNullOrWhiteSpace(result.CombinedOutput) ? $"mount ExitCode={result.ExitCode}" : result.CombinedOutput;
        if (LooksLikePermissionError(message))
        {
            message += ". Bitte als root ausführen oder passende sudo-/fstab-Optionen konfigurieren.";
        }

        _logger.Warning($"Mount-Reconnect {mountPoint}: {message}");
        return Failure("Mount-Reconnect fehlgeschlagen", message);
    }

    private static DriveInfo? FindBestDrive(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return DriveInfo.GetDrives()
            .Where(drive => IsSameOrChild(fullPath, drive.Name))
            .OrderByDescending(drive => drive.Name.Length)
            .FirstOrDefault();
    }

    private static bool IsSameOrChild(string path, string root)
    {
        var normalizedRoot = root.TrimEnd('/');
        if (string.IsNullOrEmpty(normalizedRoot))
        {
            normalizedRoot = "/";
        }

        if (string.Equals(path.TrimEnd('/'), normalizedRoot, StringComparison.Ordinal))
        {
            return true;
        }

        return path.StartsWith(normalizedRoot.TrimEnd('/') + "/", StringComparison.Ordinal);
    }

    private static bool FstabContainsMountPoint(string mountPoint)
    {
        const string fstabPath = "/etc/fstab";
        if (!File.Exists(fstabPath))
        {
            return false;
        }

        var normalizedMount = NormalizeMountForCompare(mountPoint);
        foreach (var line in File.ReadLines(fstabPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var parts = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            if (string.Equals(NormalizeMountForCompare(DecodeFstabValue(parts[1])), normalizedMount, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string DecodeFstabValue(string value)
    {
        return value
            .Replace("\\040", " ", StringComparison.Ordinal)
            .Replace("\\011", "\t", StringComparison.Ordinal)
            .Replace("\\012", "\n", StringComparison.Ordinal)
            .Replace("\\134", "\\", StringComparison.Ordinal);
    }

    private static string NormalizeMountForCompare(string path)
    {
        var normalized = NormalizeLinuxPath(path);
        return normalized == "/" ? normalized : normalized.TrimEnd('/');
    }

    private static string NormalizeLinuxPath(string? path)
    {
        var normalized = path?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "";
        }

        return normalized.StartsWith("/", StringComparison.Ordinal)
            ? Path.GetFullPath(normalized)
            : normalized;
    }

    private static bool LooksLikeWindowsPath(string path)
    {
        return path.StartsWith(@"\\", StringComparison.Ordinal)
               || path.Contains('\\', StringComparison.Ordinal)
               || (path.Length >= 2 && path[1] == ':');
    }

    private static bool LooksLikePermissionError(string message)
    {
        return message.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
               || message.Contains("must be superuser", StringComparison.OrdinalIgnoreCase)
               || message.Contains("only root", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal BytesToGb(long bytes)
    {
        return Math.Round((decimal)bytes / 1024 / 1024 / 1024, 1);
    }

    private static ServiceOperationResult Success(string message)
    {
        return new ServiceOperationResult { Success = true, Message = message };
    }

    private static ServiceOperationResult Failure(string category, string message)
    {
        return new ServiceOperationResult { Success = false, ErrorCategory = category, Message = message };
    }
}
