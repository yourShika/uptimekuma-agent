using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UptimeKumaTrayAgent.Models;

namespace UptimeKumaTrayAgent.Services;

public sealed class WindowsServiceManager
{
    private const uint ScManagerConnect = 0x0001;
    private const uint ScManagerEnumerateService = 0x0004;
    private const uint ServiceQueryStatus = 0x0004;
    private const uint ServiceStart = 0x0010;
    private const uint ServiceStop = 0x0020;
    private const int ServiceWin32 = 0x00000030;
    private const int ServiceStateAll = 0x00000003;
    private const int ScEnumProcessInfo = 0;
    private const int ServiceStatusProcessInfo = 0;
    private const int ServiceControlStop = 0x00000001;
    private const int ServiceAcceptStop = 0x00000001;
    private const int ErrorMoreData = 234;
    private const int ErrorServiceDoesNotExist = 1060;
    private const int ErrorAccessDenied = 5;
    private const int ErrorServiceAlreadyRunning = 1056;

    public IReadOnlyList<WindowsServiceInfo> ListServices()
    {
        var services = new List<WindowsServiceInfo>();
        var scm = OpenScManager(ScManagerEnumerateService);
        if (scm == IntPtr.Zero)
        {
            throw CreateWin32Exception("Lokale Dienste konnten nicht geladen werden");
        }

        try
        {
            var resumeHandle = 0;
            _ = EnumServicesStatusEx(
                scm,
                ScEnumProcessInfo,
                ServiceWin32,
                ServiceStateAll,
                IntPtr.Zero,
                0,
                out var bytesNeeded,
                out _,
                ref resumeHandle,
                null);

            var error = Marshal.GetLastWin32Error();
            if (bytesNeeded <= 0 && error != ErrorMoreData)
            {
                throw CreateWin32Exception("Lokale Dienste konnten nicht aufgelistet werden");
            }

            var buffer = Marshal.AllocHGlobal(bytesNeeded);
            try
            {
                resumeHandle = 0;
                if (!EnumServicesStatusEx(
                        scm,
                        ScEnumProcessInfo,
                        ServiceWin32,
                        ServiceStateAll,
                        buffer,
                        bytesNeeded,
                        out _,
                        out var servicesReturned,
                        ref resumeHandle,
                        null))
                {
                    throw CreateWin32Exception("Lokale Dienste konnten nicht aufgelistet werden");
                }

                var structSize = Marshal.SizeOf<EnumServiceStatusProcess>();
                for (var i = 0; i < servicesReturned; i++)
                {
                    var itemPtr = IntPtr.Add(buffer, i * structSize);
                    var item = Marshal.PtrToStructure<EnumServiceStatusProcess>(itemPtr);
                    services.Add(new WindowsServiceInfo
                    {
                        ServiceName = item.ServiceName ?? "",
                        DisplayName = item.DisplayName ?? item.ServiceName ?? "",
                        Status = MapState(item.ServiceStatusProcess.CurrentState),
                        CanStop = (item.ServiceStatusProcess.ControlsAccepted & ServiceAcceptStop) == ServiceAcceptStop
                    });
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }

        return services
            .OrderBy(service => service.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public ServiceOperationResult GetStatus(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return Failure("Dienst existiert nicht", "Dienstname fehlt");
        }

        var open = OpenServiceFor(serviceName, ServiceQueryStatus, out var serviceHandle, out var openResult);
        if (!open)
        {
            return openResult;
        }

        try
        {
            var status = QueryStatus(serviceHandle);
            return new ServiceOperationResult
            {
                Success = true,
                Status = MapState(status.CurrentState),
                Message = "Dienststatus gelesen"
            };
        }
        catch (Exception ex)
        {
            return Failure("Dienstprüfung fehlgeschlagen", ex.Message);
        }
        finally
        {
            CloseServiceHandle(serviceHandle);
        }
    }

    public Task<ServiceOperationResult> StartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return Task.Run(() => StartServiceCore(serviceName, timeout, cancellationToken), cancellationToken);
    }

    public Task<ServiceOperationResult> StopServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken, bool forceKillOnTimeout = false)
    {
        return Task.Run(() => StopServiceCore(serviceName, timeout, cancellationToken, forceKillOnTimeout), cancellationToken);
    }

    public async Task<ServiceOperationResult> RestartServiceAsync(
        string serviceName,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool forceKillOnStopTimeout = false)
    {
        var stop = await StopServiceAsync(serviceName, timeout, cancellationToken, forceKillOnStopTimeout).ConfigureAwait(false);
        if (!stop.Success && !string.Equals(stop.Status, "Stopped", StringComparison.OrdinalIgnoreCase))
        {
            return stop;
        }

        return await StartServiceAsync(serviceName, timeout, cancellationToken).ConfigureAwait(false);
    }

    private ServiceOperationResult StartServiceCore(string serviceName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!OpenServiceFor(serviceName, ServiceQueryStatus | ServiceStart, out var serviceHandle, out var openResult))
        {
            return openResult;
        }

        try
        {
            var current = QueryStatus(serviceHandle);
            if (current.CurrentState == ServiceState.Running)
            {
                return Success("Running", "Dienst läuft bereits");
            }

            if (!StartService(serviceHandle, 0, IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                if (error != ErrorServiceAlreadyRunning)
                {
                    return Failure(MapStartError(error), new Win32Exception(error).Message, MapState(current.CurrentState));
                }
            }

            return WaitForStatus(
                serviceHandle,
                ServiceState.Running,
                timeout,
                cancellationToken,
                "Dienst gestartet",
                "Timeout beim Starten des Dienstes");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Failure("Dienst konnte nicht gestartet werden", ex.Message);
        }
        finally
        {
            CloseServiceHandle(serviceHandle);
        }
    }

    private ServiceOperationResult StopServiceCore(
        string serviceName,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool forceKillOnTimeout)
    {
        if (!OpenServiceFor(serviceName, ServiceQueryStatus | ServiceStop, out var serviceHandle, out var openResult))
        {
            return openResult;
        }

        try
        {
            var current = QueryStatus(serviceHandle);
            if (current.CurrentState == ServiceState.Stopped)
            {
                return Success("Stopped", "Dienst ist bereits gestoppt");
            }

            if ((current.ControlsAccepted & ServiceAcceptStop) != ServiceAcceptStop)
            {
                return Failure("Dienst konnte nicht gestoppt werden", "Dienst akzeptiert kein Stop-Signal", MapState(current.CurrentState));
            }

            var nativeStatus = new ServiceStatus();
            if (!ControlService(serviceHandle, ServiceControlStop, ref nativeStatus))
            {
                var error = Marshal.GetLastWin32Error();
                return Failure(MapStartError(error), new Win32Exception(error).Message, MapState(current.CurrentState));
            }

            var stopped = WaitForStatus(
                serviceHandle,
                ServiceState.Stopped,
                timeout,
                cancellationToken,
                "Dienst gestoppt",
                "Timeout beim Stoppen des Dienstes");
            if (stopped.Success || !forceKillOnTimeout)
            {
                return stopped;
            }

            var timedOutStatus = QueryStatus(serviceHandle);
            var processId = timedOutStatus.ProcessId != 0 ? timedOutStatus.ProcessId : current.ProcessId;
            if (processId == 0)
            {
                return stopped;
            }

            var killed = ForceKillServiceProcess(processId, serviceName);
            if (!killed.Success)
            {
                return killed;
            }

            var afterKill = WaitForStatus(
                serviceHandle,
                ServiceState.Stopped,
                TimeSpan.FromSeconds(10),
                cancellationToken,
                $"Dienstprozess {processId} wurde erzwungen beendet",
                "Dienstprozess beendet, Status unklar");

            return afterKill.Success
                ? afterKill
                : Failure(afterKill.ErrorCategory, $"{killed.Message}; {afterKill.Message}", afterKill.Status);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Failure("Dienst konnte nicht gestoppt werden", ex.Message);
        }
        finally
        {
            CloseServiceHandle(serviceHandle);
        }
    }

    private bool OpenServiceFor(string serviceName, uint access, out IntPtr serviceHandle, out ServiceOperationResult result)
    {
        serviceHandle = IntPtr.Zero;
        result = Failure("Dienstprüfung fehlgeschlagen", "");

        var scm = OpenScManager(ScManagerConnect);
        if (scm == IntPtr.Zero)
        {
            result = Failure("keine Rechte zum Lesen des Dienstes", CreateWin32Exception("Service Control Manager konnte nicht geöffnet werden").Message);
            return false;
        }

        try
        {
            serviceHandle = OpenService(scm, serviceName, access);
            if (serviceHandle != IntPtr.Zero)
            {
                return true;
            }

            var error = Marshal.GetLastWin32Error();
            result = error switch
            {
                ErrorServiceDoesNotExist => Failure("Dienst existiert nicht", $"Dienst {serviceName} existiert nicht"),
                ErrorAccessDenied => Failure((access & (ServiceStart | ServiceStop)) != 0
                    ? "keine Rechte zum Starten des Dienstes"
                    : "keine Rechte zum Lesen des Dienstes", new Win32Exception(error).Message),
                _ => Failure("Dienstprüfung fehlgeschlagen", new Win32Exception(error).Message)
            };
            return false;
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    private static ServiceOperationResult WaitForStatus(
        IntPtr serviceHandle,
        ServiceState desired,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string successMessage,
        string timeoutCategory)
    {
        var deadline = DateTimeOffset.Now + timeout;
        ServiceStatusProcess status;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            status = QueryStatus(serviceHandle);
            if (status.CurrentState == desired)
            {
                return Success(MapState(status.CurrentState), successMessage);
            }

            Thread.Sleep(500);
        } while (DateTimeOffset.Now < deadline);

        return Failure(timeoutCategory, $"Dienststatus ist {MapState(status.CurrentState)}", MapState(status.CurrentState));
    }

    private static ServiceOperationResult ForceKillServiceProcess(uint processId, string serviceName)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            process.Kill(entireProcessTree: true);
            if (!process.WaitForExit(10000))
            {
                return Failure("Dienstprozess konnte nicht beendet werden", $"Prozess {processId} für Dienst {serviceName} reagiert nicht", "StopPending");
            }

            return Success("Stopped", $"Dienstprozess {processId} für {serviceName} wurde erzwungen beendet");
        }
        catch (ArgumentException)
        {
            return Success("Stopped", $"Dienstprozess {processId} für {serviceName} existiert nicht mehr");
        }
        catch (Win32Exception ex)
        {
            return Failure("keine Rechte zum Beenden des Dienstprozesses", ex.Message, "StopPending");
        }
        catch (Exception ex)
        {
            return Failure("Dienstprozess konnte nicht beendet werden", ex.Message, "StopPending");
        }
    }

    private static ServiceStatusProcess QueryStatus(IntPtr serviceHandle)
    {
        var size = Marshal.SizeOf<ServiceStatusProcess>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!QueryServiceStatusEx(serviceHandle, ServiceStatusProcessInfo, buffer, size, out _))
            {
                throw CreateWin32Exception("Dienststatus konnte nicht gelesen werden");
            }

            return Marshal.PtrToStructure<ServiceStatusProcess>(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IntPtr OpenScManager(uint access)
    {
        return OpenSCManager(null, null, access);
    }

    private static Win32Exception CreateWin32Exception(string prefix)
    {
        var error = Marshal.GetLastWin32Error();
        return new Win32Exception(error, $"{prefix}: {new Win32Exception(error).Message}");
    }

    private static ServiceOperationResult Success(string status, string message)
    {
        return new ServiceOperationResult { Success = true, Status = status, Message = message };
    }

    private static ServiceOperationResult Failure(string category, string message, string status = "")
    {
        return new ServiceOperationResult { Success = false, ErrorCategory = category, Message = message, Status = status };
    }

    private static string MapStartError(int error)
    {
        return error == ErrorAccessDenied ? "keine Rechte zum Starten des Dienstes" : "Dienst konnte nicht gestartet werden";
    }

    private static string MapState(ServiceState state)
    {
        return state switch
        {
            ServiceState.Stopped => "Stopped",
            ServiceState.StartPending => "StartPending",
            ServiceState.StopPending => "StopPending",
            ServiceState.Running => "Running",
            ServiceState.ContinuePending => "ContinuePending",
            ServiceState.PausePending => "PausePending",
            ServiceState.Paused => "Paused",
            _ => "Unknown"
        };
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr serviceHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool EnumServicesStatusEx(
        IntPtr serviceManager,
        int infoLevel,
        int serviceType,
        int serviceState,
        IntPtr services,
        int bufferSize,
        out int bytesNeeded,
        out int servicesReturned,
        ref int resumeHandle,
        string? groupName);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenService(IntPtr serviceManager, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatusEx(
        IntPtr serviceHandle,
        int infoLevel,
        IntPtr buffer,
        int bufferSize,
        out int bytesNeeded);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool StartService(IntPtr serviceHandle, int numberOfServiceArgs, IntPtr serviceArgVectors);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool ControlService(IntPtr serviceHandle, int control, ref ServiceStatus serviceStatus);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct EnumServiceStatusProcess
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? ServiceName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? DisplayName;

        public ServiceStatusProcess ServiceStatusProcess;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatusProcess
    {
        public uint ServiceType;
        public ServiceState CurrentState;
        public int ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
        public uint ProcessId;
        public uint ServiceFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus
    {
        public uint ServiceType;
        public ServiceState CurrentState;
        public int ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
    }

    private enum ServiceState : uint
    {
        Stopped = 1,
        StartPending = 2,
        StopPending = 3,
        Running = 4,
        ContinuePending = 5,
        PausePending = 6,
        Paused = 7
    }
}
