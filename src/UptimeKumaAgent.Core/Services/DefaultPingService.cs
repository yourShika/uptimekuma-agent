using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UptimeKumaTrayAgent.Models;

namespace UptimeKumaTrayAgent.Services;

public sealed class DefaultPingService : IPingService
{
    public async Task<MonitorCheckResult> PingAsync(PingRequest request, CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var reply = await ping
                .SendPingAsync(request.Host, request.TimeoutMs)
                .WaitAsync(TimeSpan.FromMilliseconds(request.TimeoutMs), cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();
            if (reply.Status == IPStatus.Success)
            {
                return MonitorCheckResult.Up($"Ping OK: {request.Name} erreichbar", Math.Max(1, reply.RoundtripTime));
            }

            return MonitorCheckResult.Down(
                $"Ping Fehler: {request.Name} nicht erreichbar",
                MapPingStatus(reply.Status),
                stopwatch.ElapsedMilliseconds);
        }
        catch (TimeoutException)
        {
            return MonitorCheckResult.Down($"Ping Fehler: {request.Name} Timeout", "Ping fehlgeschlagen", stopwatch.ElapsedMilliseconds);
        }
        catch (PingException ex) when (FindException<SocketException>(ex) is { } socket)
        {
            return MonitorCheckResult.Down($"Ping Fehler: {request.Name} {socket.Message}", MapSocketErrorForPing(socket), stopwatch.ElapsedMilliseconds);
        }
        catch (SocketException ex)
        {
            return MonitorCheckResult.Down($"Ping Fehler: {request.Name} {ex.Message}", MapSocketErrorForPing(ex), stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return MonitorCheckResult.Down($"Ping Fehler: {request.Name} {ex.Message}", "Ping fehlgeschlagen", stopwatch.ElapsedMilliseconds);
        }
    }

    private static string MapPingStatus(IPStatus status)
    {
        return status switch
        {
            IPStatus.TimedOut => "Ping fehlgeschlagen",
            IPStatus.DestinationHostUnreachable => "Zielsystem offline",
            IPStatus.DestinationNetworkUnreachable => "Zielsystem offline",
            _ => "Ping fehlgeschlagen"
        };
    }

    private static string MapSocketErrorForPing(SocketException exception)
    {
        return exception.SocketErrorCode switch
        {
            SocketError.HostNotFound or SocketError.NoData => "DNS-Problem",
            SocketError.TimedOut => "Ping fehlgeschlagen",
            SocketError.NetworkUnreachable or SocketError.HostUnreachable => "Zielsystem offline",
            _ => "Ping fehlgeschlagen"
        };
    }

    private static T? FindException<T>(Exception exception) where T : Exception
    {
        var current = exception;
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = current.InnerException!;
        }

        return null;
    }
}
