using System.Formats.Tar;
using System.IO.Compression;
using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Services;

namespace UptimeKumaAgent.Linux;

internal sealed class LinuxUpdateInstaller
{
    private readonly GitHubUpdateService _updateService;
    private readonly Logger _logger;

    public LinuxUpdateInstaller(GitHubUpdateService updateService, Logger logger)
    {
        _updateService = updateService;
        _logger = logger;
    }

    public async Task<ServiceOperationResult> DownloadAndInstallAsync(UpdateCheckResult update, CancellationToken cancellationToken)
    {
        if (update.Asset is null)
        {
            return Failure("UpdateAssetMissing", "Für diese Plattform wurde kein passendes Linux-Paket im GitHub Release gefunden.");
        }

        if (!OperatingSystem.IsLinux())
        {
            return Failure("UnsupportedPlatform", "Linux-Updates können nur auf Linux installiert werden.");
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "uptime-kuma-agent-update-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            var archivePath = await _updateService
                .DownloadAssetAsync(update.Asset, tempRoot, progress: null, cancellationToken)
                .ConfigureAwait(false);

            var extractedBinary = Path.Combine(tempRoot, "uptime-kuma-agent");
            await ExtractAgentBinaryAsync(archivePath, extractedBinary, cancellationToken).ConfigureAwait(false);

            var installPath = Path.Combine(LinuxAppPaths.DefaultInstallDirectory, "uptime-kuma-agent");
            await ReplaceInstalledBinaryAsync(extractedBinary, installPath, cancellationToken).ConfigureAwait(false);
            _logger.Info("Linux-Agent wurde aktualisiert: " + installPath);

            var restart = await ProcessRunner
                .RunAsync("systemctl", new[] { "restart", "uptime-kuma-agent.service" }, cancellationToken)
                .ConfigureAwait(false);

            if (restart.ExitCode == 0)
            {
                return Success("Installed", "Update installiert und uptime-kuma-agent.service neu gestartet.");
            }

            var restartMessage = string.IsNullOrWhiteSpace(restart.CombinedOutput)
                ? "systemctl ExitCode=" + restart.ExitCode
                : restart.CombinedOutput;
            if (restart.ExitCode == 127)
            {
                return Success("InstalledRestartSkipped", "Update installiert. systemctl wurde nicht gefunden; bitte den Agent manuell neu starten.");
            }

            if (IsPermissionError(restartMessage))
            {
                return Success("InstalledRestartNeedsRoot", "Update installiert. Der Dienst konnte nicht neu gestartet werden, weil Root-/sudo-Rechte fehlen.");
            }

            return Success("InstalledRestartFailed", "Update installiert. Neustart des Dienstes fehlgeschlagen: " + restartMessage);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Failure("PermissionDenied", "Für das Update nach /opt/uptime-kuma-agent werden Root-/sudo-Rechte benötigt: " + ex.Message);
        }
        catch (IOException ex) when (IsPermissionError(ex.Message))
        {
            return Failure("PermissionDenied", "Für das Update nach /opt/uptime-kuma-agent werden Root-/sudo-Rechte benötigt: " + ex.Message);
        }
        catch (Exception ex)
        {
            _logger.Error("Linux-Update fehlgeschlagen", ex);
            return Failure("UpdateFailed", ex.Message);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task ExtractAgentBinaryAsync(string archivePath, string destinationPath, CancellationToken cancellationToken)
    {
        await using var file = File.OpenRead(archivePath);
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new TarReader(gzip, leaveOpen: false);

        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            if (!string.Equals(Path.GetFileName(entry.Name), "uptime-kuma-agent", StringComparison.Ordinal)
                || entry.DataStream is null)
            {
                continue;
            }

            await using var output = File.Create(destinationPath);
            await entry.DataStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new InvalidDataException("Das Linux-Updatepaket enthält keine uptime-kuma-agent Binary.");
    }

    private static async Task ReplaceInstalledBinaryAsync(string sourceBinary, string installPath, CancellationToken cancellationToken)
    {
        var installDirectory = Path.GetDirectoryName(installPath) ?? LinuxAppPaths.DefaultInstallDirectory;
        if (!Directory.Exists(installDirectory))
        {
            throw new DirectoryNotFoundException($"Installationsordner fehlt: {installDirectory}. Bitte zuerst InstallLinux.sh ausführen.");
        }

        var tempTarget = installPath + ".new";
        try
        {
            if (File.Exists(tempTarget))
            {
                File.Delete(tempTarget);
            }

            await using (var source = File.OpenRead(sourceBinary))
            await using (var target = File.Create(tempTarget))
            {
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            }

            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(
                    tempTarget,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            File.Move(tempTarget, installPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempTarget))
                {
                    File.Delete(tempTarget);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    private static bool IsPermissionError(string message)
    {
        return message.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("access denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("authentication is required", StringComparison.OrdinalIgnoreCase)
            || message.Contains("interactive authentication required", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not authorized", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Temporary update files must not hide the real update result.
        }
    }

    private static ServiceOperationResult Success(string status, string message)
    {
        return new ServiceOperationResult { Success = true, Status = status, Message = message };
    }

    private static ServiceOperationResult Failure(string category, string message)
    {
        return new ServiceOperationResult { Success = false, ErrorCategory = category, Message = message };
    }
}
