using System.Net;
using UptimeKumaTrayAgent.Models;

namespace UptimeKumaTrayAgent.Services;

public interface IServiceManager
{
    ServiceOperationResult GetStatus(string serviceName);
    Task<ServiceOperationResult> StartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken);
    Task<ServiceOperationResult> StopServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken, bool forceKillOnTimeout = false);
    Task<ServiceOperationResult> RestartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken, bool forceKillOnStopTimeout = false);
}

public interface IDriveMonitorService
{
    Task<DriveCheckResult> CheckAsync(DriveCheckConfig check, CancellationToken cancellationToken);
}

public interface ITcpConnectionMonitor
{
    IReadOnlyList<TcpConnectionInfo> GetConnections(string host, int port, string direction);
}

public interface IPingService
{
    Task<MonitorCheckResult> PingAsync(PingRequest request, CancellationToken cancellationToken);
}

public sealed record PingRequest(string Host, string Name, int TimeoutMs);

public sealed record DriveCheckResult(
    bool IsReady,
    string Path,
    string Category,
    string Message,
    string DriveType,
    string Format,
    long TotalBytes,
    long FreeBytes,
    int FreePercent,
    bool ReconnectAttempted,
    bool ReconnectSuccess,
    string ReconnectMessage)
{
    public static DriveCheckResult Up(
        string path,
        string driveType,
        string format,
        long totalBytes,
        long freeBytes,
        int freePercent,
        string message)
    {
        return new DriveCheckResult(true, path, "", message, driveType, format, totalBytes, freeBytes, freePercent, false, false, "");
    }

    public static DriveCheckResult Down(
        string path,
        string category,
        string message,
        long totalBytes = 0,
        long freeBytes = 0,
        int freePercent = 0,
        string format = "",
        string driveType = "")
    {
        return new DriveCheckResult(false, path, category, message, driveType, format, totalBytes, freeBytes, freePercent, false, false, "");
    }

    public string BuildDetailText()
    {
        var totalGb = TotalBytes > 0 ? $"{(decimal)TotalBytes / 1024 / 1024 / 1024:N1} GB" : "-";
        var freeGb = FreeBytes > 0 ? $"{(decimal)FreeBytes / 1024 / 1024 / 1024:N1} GB" : "-";
        var reconnect = ReconnectAttempted
            ? $" | Reconnect={(ReconnectSuccess ? "OK" : "Fehler")} {ReconnectMessage}"
            : "";
        return $"Pfad={Path} Typ={DriveType} Format={Format} Gesamt={totalGb} Frei={freeGb} FreiProzent={FreePercent}%{reconnect}";
    }
}

public sealed record DriveSnapshot(
    string Path,
    string DriveType,
    bool IsReady,
    string Format,
    long TotalBytes,
    long FreeBytes,
    string NetworkPath);

public sealed record TcpConnectionInfo(
    string Direction,
    string AddressFamily,
    IPAddress LocalAddress,
    int LocalPort,
    IPAddress RemoteAddress,
    int RemotePort,
    string State,
    int ProcessId,
    string ProcessName)
{
    public string ToLogText()
    {
        var process = ProcessId > 0
            ? $"PID={ProcessId} Prozess={ProcessName}"
            : "PID=-";
        return $"{Direction} {AddressFamily} Local={LocalAddress}:{LocalPort} Remote={RemoteAddress}:{RemotePort} Status={State} {process}";
    }
}

public sealed class NullTcpConnectionMonitor : ITcpConnectionMonitor
{
    public IReadOnlyList<TcpConnectionInfo> GetConnections(string host, int port, string direction)
    {
        return Array.Empty<TcpConnectionInfo>();
    }
}
