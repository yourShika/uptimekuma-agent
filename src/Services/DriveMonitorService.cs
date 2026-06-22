using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UptimeKumaTrayAgent.Models;

namespace UptimeKumaTrayAgent.Services;

public sealed class DriveMonitorService : IDriveMonitorService
{
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

    public IReadOnlyList<DriveSnapshot> ListDrives()
    {
        return DriveInfo.GetDrives()
            .Select(drive =>
            {
                try
                {
                    return new DriveSnapshot(
                        drive.Name,
                        drive.DriveType.ToString(),
                        drive.IsReady,
                        drive.IsReady ? drive.DriveFormat : "",
                        drive.IsReady ? drive.TotalSize : 0,
                        drive.IsReady ? drive.AvailableFreeSpace : 0,
                        TryGetConnectionPath(drive.Name));
                }
                catch (Exception ex)
                {
                    return new DriveSnapshot(drive.Name, drive.DriveType.ToString(), false, "", 0, 0, ex.Message);
                }
            })
            .OrderBy(drive => drive.Path, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static DriveCheckResult CheckCore(DriveCheckConfig check)
    {
        var path = DriveCheckConfig.NormalizePath(check.Path);
        if (string.IsNullOrWhiteSpace(path))
        {
            return DriveCheckResult.Down(path, "Pfad fehlt", "Laufwerkspfad fehlt");
        }

        try
        {
            if (IsUncPath(path))
            {
                var exists = Directory.Exists(path);
                return exists
                    ? DriveCheckResult.Up(path, "Network", "UNC erreichbar", 0, 0, 0, "UNC-Pfad erreichbar")
                    : DriveCheckResult.Down(path, "UNC-Pfad nicht erreichbar", "UNC-Pfad ist nicht erreichbar");
            }

            var root = Path.GetPathRoot(path);
            if (string.IsNullOrWhiteSpace(root))
            {
                return DriveCheckResult.Down(path, "Laufwerkspfad ungültig", "Laufwerkspfad ist ungültig");
            }

            var drive = new DriveInfo(root);
            if (!drive.IsReady)
            {
                return DriveCheckResult.Down(root, "Laufwerk nicht bereit", $"{root} ist nicht bereit");
            }

            var freePercent = drive.TotalSize > 0
                ? (int)Math.Round((double)drive.AvailableFreeSpace / drive.TotalSize * 100)
                : 0;
            var freeGb = BytesToGb(drive.AvailableFreeSpace);

            if (check.MinimumFreePercent > 0 && freePercent < check.MinimumFreePercent)
            {
                return DriveCheckResult.Down(
                    root,
                    "Zu wenig freier Speicher",
                    $"{root} hat nur {freePercent}% frei, erwartet mindestens {check.MinimumFreePercent}%",
                    drive.TotalSize,
                    drive.AvailableFreeSpace,
                    freePercent,
                    drive.DriveFormat,
                    drive.DriveType.ToString());
            }

            if (check.MinimumFreeGb > 0 && freeGb < check.MinimumFreeGb)
            {
                return DriveCheckResult.Down(
                    root,
                    "Zu wenig freier Speicher",
                    $"{root} hat nur {freeGb:N1} GB frei, erwartet mindestens {check.MinimumFreeGb:N1} GB",
                    drive.TotalSize,
                    drive.AvailableFreeSpace,
                    freePercent,
                    drive.DriveFormat,
                    drive.DriveType.ToString());
            }

            return DriveCheckResult.Up(
                root,
                drive.DriveType.ToString(),
                drive.DriveFormat,
                drive.TotalSize,
                drive.AvailableFreeSpace,
                freePercent,
                $"{root} bereit, {freePercent}% frei ({freeGb:N1} GB)");
        }
        catch (Exception ex)
        {
            return DriveCheckResult.Down(path, "Laufwerksprüfung fehlgeschlagen", ex.Message);
        }
    }

    private static async Task<ServiceOperationResult> TryReconnectAsync(DriveCheckConfig check, CancellationToken cancellationToken)
    {
        var driveName = NormalizeDriveName(check.Path);
        var reconnectPath = string.IsNullOrWhiteSpace(check.ReconnectPath)
            ? TryGetConnectionPath(driveName)
            : check.ReconnectPath.Trim();

        if (string.IsNullOrWhiteSpace(driveName) || string.IsNullOrWhiteSpace(reconnectPath))
        {
            return new ServiceOperationResult
            {
                Success = false,
                ErrorCategory = "Netzlaufwerk kann nicht verbunden werden",
                Message = "Laufwerksbuchstabe oder UNC-Pfad fehlt"
            };
        }

        var info = new ProcessStartInfo
        {
            FileName = "net.exe",
            Arguments = $"use {Quote(driveName)} {Quote(reconnectPath)} /persistent:yes",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(info);
        if (process is null)
        {
            return new ServiceOperationResult { Success = false, ErrorCategory = "Reconnect fehlgeschlagen", Message = "net.exe konnte nicht gestartet werden" };
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var output = (await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false)).Trim();
        var error = (await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false)).Trim();
        var message = string.IsNullOrWhiteSpace(error) ? output : error;
        return new ServiceOperationResult
        {
            Success = process.ExitCode == 0,
            ErrorCategory = process.ExitCode == 0 ? "" : "Reconnect fehlgeschlagen",
            Message = string.IsNullOrWhiteSpace(message) ? $"net use ExitCode={process.ExitCode}" : message
        };
    }

    private static string NormalizeDriveName(string path)
    {
        var normalized = DriveCheckConfig.NormalizePath(path);
        return normalized.Length >= 2 && normalized[1] == ':'
            ? normalized[..2]
            : "";
    }

    private static string TryGetConnectionPath(string path)
    {
        var driveName = NormalizeDriveName(path);
        if (string.IsNullOrWhiteSpace(driveName))
        {
            return "";
        }

        var capacity = 512;
        var builder = new StringBuilder(capacity);
        var result = WNetGetConnection(driveName, builder, ref capacity);
        if (result == 0)
        {
            return builder.ToString();
        }

        return "";
    }

    private static bool IsUncPath(string path)
    {
        return path.StartsWith(@"\\", StringComparison.Ordinal);
    }

    private static decimal BytesToGb(long bytes)
    {
        return Math.Round((decimal)bytes / 1024 / 1024 / 1024, 1);
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetGetConnection(string localName, StringBuilder remoteName, ref int length);
}
