using System.Net;
using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Services;

namespace UptimeKumaAgent.Linux;

internal sealed class LinuxTcpConnectionMonitor : ITcpConnectionMonitor
{
    private readonly Logger _logger;

    public LinuxTcpConnectionMonitor(Logger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<TcpConnectionInfo> GetConnections(string host, int port, string direction)
    {
        try
        {
            var result = ProcessRunner
                .RunAsync("ss", new[] { "-tanp" }, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (result.ExitCode != 0)
            {
                if (result.ExitCode == 127)
                {
                    _logger.Debug("TCP-Verbindungsdetails übersprungen: ss ist nicht installiert");
                }
                else
                {
                    _logger.Debug("TCP-Verbindungsdetails übersprungen: " + result.CombinedOutput);
                }

                return Array.Empty<TcpConnectionInfo>();
            }

            var targetAddresses = ResolveTargetAddresses(host);
            var includeIncoming = TcpConnectionLogDirections.IncludesIncoming(direction);
            var includeOutgoing = TcpConnectionLogDirections.IncludesOutgoing(direction);
            return result.StandardOutput
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .Select(ParseLine)
                .Where(connection => connection is not null)
                .Cast<TcpConnectionInfo>()
                .Select(connection => Classify(connection, port, targetAddresses, includeIncoming, includeOutgoing))
                .Where(connection => connection is not null)
                .Cast<TcpConnectionInfo>()
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.Debug("TCP-Verbindungsdetails konnten nicht gelesen werden: " + ex.Message);
            return Array.Empty<TcpConnectionInfo>();
        }
    }

    private static TcpConnectionInfo? ParseLine(string line)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
        {
            return null;
        }

        var state = parts[0];
        var local = ParseEndpoint(parts[3]);
        var remote = ParseEndpoint(parts[4]);
        if (local is null || remote is null)
        {
            return null;
        }

        var processText = parts.Length > 5 ? string.Join(" ", parts.Skip(5)) : "";
        var processId = TryExtractPid(processText);
        return new TcpConnectionInfo(
            "",
            local.Value.Address.AddressFamily.ToString(),
            local.Value.Address,
            local.Value.Port,
            remote.Value.Address,
            remote.Value.Port,
            NormalizeState(state),
            processId,
            processText);
    }

    private static TcpConnectionInfo? Classify(
        TcpConnectionInfo connection,
        int port,
        IReadOnlySet<IPAddress> targetAddresses,
        bool includeIncoming,
        bool includeOutgoing)
    {
        var incoming = connection.LocalPort == port;
        var outgoing = connection.RemotePort == port
            && (targetAddresses.Count == 0 || targetAddresses.Contains(connection.RemoteAddress));

        if (incoming && outgoing && includeIncoming && includeOutgoing)
        {
            return connection with { Direction = "Intern" };
        }

        if (incoming && includeIncoming)
        {
            var direction = string.Equals(connection.State, "Listen", StringComparison.OrdinalIgnoreCase)
                ? "Eingehend Listener"
                : "Eingehend";
            return connection with { Direction = direction };
        }

        if (outgoing && includeOutgoing)
        {
            return connection with { Direction = "Ausgehend" };
        }

        return null;
    }

    private static (IPAddress Address, int Port)? ParseEndpoint(string value)
    {
        var trimmed = value.Trim();
        if (trimmed is "*" or "-")
        {
            return (IPAddress.Any, 0);
        }

        var separator = trimmed.LastIndexOf(':');
        if (separator < 0 || separator == trimmed.Length - 1)
        {
            return null;
        }

        var rawAddress = trimmed[..separator].Trim('[', ']');
        var rawPort = trimmed[(separator + 1)..];
        if (!int.TryParse(rawPort, out var port))
        {
            return null;
        }

        if (rawAddress is "*" or "0.0.0.0")
        {
            return (IPAddress.Any, port);
        }

        if (rawAddress is "::" or "[::]")
        {
            return (IPAddress.IPv6Any, port);
        }

        return IPAddress.TryParse(rawAddress, out var address)
            ? (address, port)
            : (IPAddress.None, port);
    }

    private static IReadOnlySet<IPAddress> ResolveTargetAddresses(string host)
    {
        var addresses = new HashSet<IPAddress>();
        if (string.IsNullOrWhiteSpace(host))
        {
            return addresses;
        }

        if (IPAddress.TryParse(host, out var parsed))
        {
            addresses.Add(parsed);
            return addresses;
        }

        try
        {
            foreach (var address in Dns.GetHostAddresses(host))
            {
                addresses.Add(address);
            }
        }
        catch
        {
            // DNS failures must not break TCP logging.
        }

        return addresses;
    }

    private static int TryExtractPid(string processText)
    {
        const string pidPrefix = "pid=";
        var index = processText.IndexOf(pidPrefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return 0;
        }

        var start = index + pidPrefix.Length;
        var end = start;
        while (end < processText.Length && char.IsDigit(processText[end]))
        {
            end++;
        }

        return int.TryParse(processText[start..end], out var pid) ? pid : 0;
    }

    private static string NormalizeState(string state)
    {
        return state.Equals("LISTEN", StringComparison.OrdinalIgnoreCase) ? "Listen" : state;
    }
}
