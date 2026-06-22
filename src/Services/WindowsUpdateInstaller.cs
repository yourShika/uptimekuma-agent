using System.ComponentModel;
using System.Diagnostics;
using UptimeKumaTrayAgent.Models;

namespace UptimeKumaTrayAgent.Services;

public sealed class WindowsUpdateInstaller
{
    private readonly GitHubUpdateService _updateService;
    private readonly Logger _logger;

    public WindowsUpdateInstaller(GitHubUpdateService updateService, Logger logger)
    {
        _updateService = updateService;
        _logger = logger;
    }

    public async Task<ServiceOperationResult> DownloadAndStartInstallerAsync(
        UpdateCheckResult update,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (update.Asset is null)
        {
            return Failure("UpdateAssetMissing", "Für diese Plattform wurde kein passendes MSI-Paket im GitHub Release gefunden.");
        }

        try
        {
            var downloadDirectory = Path.Combine(Path.GetTempPath(), "UptimeKumaTrayAgent", "updates");
            var installerPath = await _updateService
                .DownloadAssetAsync(update.Asset, downloadDirectory, progress, cancellationToken)
                .ConfigureAwait(false);

            _logger.Info("Updatepaket heruntergeladen: " + Path.GetFileName(installerPath));

            var startInfo = new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(installerPath) ?? downloadDirectory
            };
            startInfo.ArgumentList.Add("/i");
            startInfo.ArgumentList.Add(installerPath);
            startInfo.ArgumentList.Add("/passive");

            var process = Process.Start(startInfo);
            return process is null
                ? Failure("InstallerStartFailed", "Der MSI-Installer konnte nicht gestartet werden.")
                : Success("InstallerStarted", "Der MSI-Installer wurde gestartet.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return Failure("ElevationCancelled", "Das Update wurde abgebrochen, weil die Administratorbestätigung nicht erteilt wurde.");
        }
        catch (Win32Exception ex)
        {
            return Failure("InstallerStartFailed", "Der MSI-Installer konnte nicht gestartet werden: " + ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Failure("PermissionDenied", "Für die Installation werden Administratorrechte benötigt: " + ex.Message);
        }
        catch (Exception ex)
        {
            _logger.Error("Update konnte nicht gestartet werden", ex);
            return Failure("UpdateFailed", ex.Message);
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
