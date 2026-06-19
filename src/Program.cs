using System.Windows.Forms;
using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Services;
using UptimeKumaTrayAgent.Utils;

namespace UptimeKumaTrayAgent;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--service", StringComparison.OrdinalIgnoreCase)))
        {
            if (!SingleInstanceGuard.TryAcquire(@"Global\UptimeKumaTrayAgent.Service", out var serviceGuard))
            {
                return;
            }

            using (serviceGuard)
            {
                new AgentWindowsService().Run();
            }

            return;
        }

        I18n.Apply(AppLanguages.System);
        if (!SingleInstanceGuard.TryAcquire(@"Global\UptimeKumaTrayAgent.Gui", out var guiGuard))
        {
            MessageBox.Show(
                I18n.T("Uptime Kuma Tray Agent läuft bereits."),
                "Uptime Kuma Tray Agent",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var guard = guiGuard;
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var paths = new AppPaths();
        var configService = new ConfigService(paths);
        var logger = new Logger(paths);
        var config = configService.Load();
        I18n.Apply(config.Global.Language);
        logger.SetLevel(config.Global.LogLevel);

        Application.ThreadException += (_, e) => logger.Error("Unbehandelte GUI-Exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                logger.Error("Unbehandelte Anwendungsexception", ex);
            }
        };

        var pushService = new KumaPushService(logger);
        var machineInfo = new MachineInfoProvider();
        var windowsServices = new WindowsServiceManager();
        var monitoring = new MonitoringService(config, logger, pushService, machineInfo, windowsServices);
        var autostart = new AutostartService();
        var serviceIsRunning = IsInstalledServiceRunning(windowsServices);

        logger.Info("Anwendung gestartet");

        var startMinimized = args.Any(arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase))
                             || config.Global.StartMinimized;

        using var form = new MainForm(config, configService, logger, monitoring, autostart, windowsServices, startMinimized);
        if (config.Global.MonitoringAutoStart && !serviceIsRunning)
        {
            monitoring.Start();
        }
        else if (serviceIsRunning)
        {
            logger.Info("Lokales GUI-Monitoring nicht gestartet, weil der Windows-Dienst läuft");
        }

        Application.Run(form);

        monitoring.Stop();
        logger.Info("Anwendung beendet");
    }

    private static bool IsInstalledServiceRunning(WindowsServiceManager windowsServices)
    {
        var status = windowsServices.GetStatus(AgentWindowsService.WindowsServiceName);
        return status.Success && string.Equals(status.Status, "Running", StringComparison.OrdinalIgnoreCase);
    }
}
