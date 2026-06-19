using System.ComponentModel;
using System.Runtime.InteropServices;

namespace UptimeKumaTrayAgent.Services;

public sealed class AgentWindowsService
{
    public const string WindowsServiceName = "UptimeKumaTrayAgent";
    public const string WindowsServiceDisplayName = "Uptime Kuma Tray Agent";

    private const int ServiceWin32OwnProcess = 0x00000010;
    private const int ServiceStopped = 0x00000001;
    private const int ServiceStartPending = 0x00000002;
    private const int ServiceStopPending = 0x00000003;
    private const int ServiceRunning = 0x00000004;
    private const int ServiceAcceptStop = 0x00000001;
    private const int ServiceAcceptShutdown = 0x00000004;
    private const int ServiceControlStop = 0x00000001;
    private const int ServiceControlShutdown = 0x00000005;
    private const int NoError = 0;

    private readonly ManualResetEventSlim _stopped = new(false);
    private readonly ServiceMainFunction _serviceMain;
    private readonly ServiceControlHandlerEx _controlHandler;
    private IntPtr _statusHandle;
    private int _checkpoint = 1;

    private AppPaths? _paths;
    private ConfigService? _configService;
    private Logger? _logger;
    private MonitoringService? _monitoring;
    private System.Threading.Timer? _configReloadTimer;
    private DateTime _lastConfigWriteTimeUtc;

    public AgentWindowsService()
    {
        _serviceMain = ServiceMain;
        _controlHandler = ServiceControlHandler;
    }

    public void Run()
    {
        var serviceTable = new[]
        {
            new ServiceTableEntry
            {
                ServiceName = WindowsServiceName,
                ServiceProc = _serviceMain
            },
            new ServiceTableEntry()
        };

        if (!StartServiceCtrlDispatcher(serviceTable))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows-Dienst konnte nicht gestartet werden");
        }
    }

    private void ServiceMain(int argc, IntPtr argv)
    {
        _statusHandle = RegisterServiceCtrlHandlerEx(WindowsServiceName, _controlHandler, IntPtr.Zero);
        if (_statusHandle == IntPtr.Zero)
        {
            return;
        }

        SetStatus(ServiceStartPending, 0, 30000);

        try
        {
            StartAgent();
            SetStatus(ServiceRunning, ServiceAcceptStop | ServiceAcceptShutdown, 0);
            _stopped.Wait();
        }
        catch (Exception ex)
        {
            _logger?.Error("Windows-Dienst konnte nicht gestartet werden", ex);
            SetStatus(ServiceStopped, 0, 0, 1);
        }
    }

    private int ServiceControlHandler(int control, int eventType, IntPtr eventData, IntPtr context)
    {
        if (control is ServiceControlStop or ServiceControlShutdown)
        {
            SetStatus(ServiceStopPending, 0, 30000);
            StopAgent(control == ServiceControlShutdown ? "Windows wird heruntergefahren" : "Windows-Dienst gestoppt");
            SetStatus(ServiceStopped, 0, 0);
            _stopped.Set();
        }

        return NoError;
    }

    private void StartAgent()
    {
        _paths = new AppPaths();
        _configService = new ConfigService(_paths);
        _logger = new Logger(_paths);

        var config = _configService.Load();
        _logger.SetLevel(config.Global.LogLevel);
        _logger.Info("Windows-Dienst gestartet");

        var pushService = new KumaPushService(_logger);
        var machineInfo = new MachineInfoProvider();
        var windowsServices = new WindowsServiceManager();
        _monitoring = new MonitoringService(config, _logger, pushService, machineInfo, windowsServices);
        ApplyMonitoringState(config);

        _lastConfigWriteTimeUtc = GetConfigWriteTimeUtc();
        _configReloadTimer = new System.Threading.Timer(
            _ => ReloadConfigIfChanged(),
            null,
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(15));
    }

    private void StopAgent(string reason)
    {
        try
        {
            _configReloadTimer?.Dispose();
            _configReloadTimer = null;
            _monitoring?.Stop();
            _logger?.Info(reason);
        }
        catch (Exception ex)
        {
            _logger?.Error("Fehler beim Stoppen des Windows-Dienstes", ex);
        }
    }

    private void ReloadConfigIfChanged()
    {
        try
        {
            if (_configService is null || _logger is null || _monitoring is null)
            {
                return;
            }

            var currentWriteTime = GetConfigWriteTimeUtc();
            if (currentWriteTime <= _lastConfigWriteTimeUtc)
            {
                return;
            }

            var config = _configService.Load();
            _logger.SetLevel(config.Global.LogLevel);
            _monitoring.ReloadConfig(config);
            ApplyMonitoringState(config);
            _lastConfigWriteTimeUtc = currentWriteTime;
            _logger.Info("Konfiguration im Windows-Dienst neu geladen");
        }
        catch (Exception ex)
        {
            _logger?.Error("Konfiguration konnte im Windows-Dienst nicht neu geladen werden", ex);
        }
    }

    private void ApplyMonitoringState(Models.AgentConfig config)
    {
        if (_monitoring is null || _logger is null)
        {
            return;
        }

        if (config.Global.MonitoringAutoStart)
        {
            if (!_monitoring.IsRunning)
            {
                _monitoring.Start();
                _logger.Info("Monitoring im Windows-Dienst aktiviert");
            }
        }
        else if (_monitoring.IsRunning)
        {
            _monitoring.Stop();
            _logger.Info("Monitoring im Windows-Dienst deaktiviert");
        }
        else
        {
            _logger.Info("Monitoring im Windows-Dienst bleibt deaktiviert");
        }
    }

    private DateTime GetConfigWriteTimeUtc()
    {
        if (_configService is null || !File.Exists(_configService.ConfigPath))
        {
            return DateTime.MinValue;
        }

        return File.GetLastWriteTimeUtc(_configService.ConfigPath);
    }

    private void SetStatus(int state, int controlsAccepted, int waitHint, int win32ExitCode = 0)
    {
        if (_statusHandle == IntPtr.Zero)
        {
            return;
        }

        var status = new ServiceStatus
        {
            ServiceType = ServiceWin32OwnProcess,
            CurrentState = state,
            ControlsAccepted = controlsAccepted,
            Win32ExitCode = win32ExitCode,
            ServiceSpecificExitCode = 0,
            CheckPoint = state is ServiceRunning or ServiceStopped ? 0 : _checkpoint++,
            WaitHint = waitHint
        };

        SetServiceStatus(_statusHandle, ref status);
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool StartServiceCtrlDispatcher(ServiceTableEntry[] serviceTable);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr RegisterServiceCtrlHandlerEx(
        string serviceName,
        ServiceControlHandlerEx handler,
        IntPtr context);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool SetServiceStatus(IntPtr serviceStatusHandle, ref ServiceStatus serviceStatus);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void ServiceMainFunction(int argc, IntPtr argv);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int ServiceControlHandlerEx(int control, int eventType, IntPtr eventData, IntPtr context);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ServiceTableEntry
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? ServiceName;

        public ServiceMainFunction? ServiceProc;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus
    {
        public int ServiceType;
        public int CurrentState;
        public int ControlsAccepted;
        public int Win32ExitCode;
        public int ServiceSpecificExitCode;
        public int CheckPoint;
        public int WaitHint;
    }
}
