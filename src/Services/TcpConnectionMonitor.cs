using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using UptimeKumaTrayAgent.Models;

namespace UptimeKumaTrayAgent.Services;

public sealed class TcpConnectionMonitor
{
    private const int AfInet = 2;
    private const int AfInet6 = 23;
    private const uint NoError = 0;
    private const uint ErrorInsufficientBuffer = 122;

    public IReadOnlyList<TcpConnectionInfo> GetConnections(string host, int port, string direction)
    {
        var targetAddresses = ResolveTargetAddresses(host);
        var includeIncoming = TcpConnectionLogDirections.IncludesIncoming(direction);
        var includeOutgoing = TcpConnectionLogDirections.IncludesOutgoing(direction);

        return GetAllConnections()
            .Select(connection => Classify(connection, port, targetAddresses, includeIncoming, includeOutgoing))
            .Where(connection => connection is not null)
            .Cast<TcpConnectionInfo>()
            .OrderBy(connection => connection.Direction, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(connection => connection.RemoteAddress.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(connection => connection.RemotePort)
            .ToArray();
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
            // DNS failures must not break the actual TCP check.
        }

        return addresses;
    }

    private static IReadOnlyList<TcpConnectionInfo> GetAllConnections()
    {
        var connections = new List<TcpConnectionInfo>();
        connections.AddRange(GetIPv4Connections());
        connections.AddRange(GetIPv6Connections());
        return connections;
    }

    private static IEnumerable<TcpConnectionInfo> GetIPv4Connections()
    {
        var bufferSize = 0;
        var result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, AfInet, TcpTableClass.OwnerPidAll, 0);
        if (result != ErrorInsufficientBuffer || bufferSize <= 0)
        {
            yield break;
        }

        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            result = GetExtendedTcpTable(buffer, ref bufferSize, true, AfInet, TcpTableClass.OwnerPidAll, 0);
            if (result != NoError)
            {
                yield break;
            }

            var rowCount = Marshal.ReadInt32(buffer);
            var rowPtr = IntPtr.Add(buffer, 4);
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
            for (var i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(IntPtr.Add(rowPtr, i * rowSize));
                var processId = (int)row.OwningPid;
                yield return new TcpConnectionInfo(
                    "",
                    "IPv4",
                    new IPAddress(row.LocalAddr),
                    ConvertPort(row.LocalPort),
                    new IPAddress(row.RemoteAddr),
                    ConvertPort(row.RemotePort),
                    MapState(row.State),
                    processId,
                    GetProcessName(processId));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IEnumerable<TcpConnectionInfo> GetIPv6Connections()
    {
        var bufferSize = 0;
        var result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, AfInet6, TcpTableClass.OwnerPidAll, 0);
        if (result != ErrorInsufficientBuffer || bufferSize <= 0)
        {
            yield break;
        }

        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            result = GetExtendedTcpTable(buffer, ref bufferSize, true, AfInet6, TcpTableClass.OwnerPidAll, 0);
            if (result != NoError)
            {
                yield break;
            }

            var rowCount = Marshal.ReadInt32(buffer);
            var rowPtr = IntPtr.Add(buffer, 4);
            var rowSize = Marshal.SizeOf<MibTcp6RowOwnerPid>();
            for (var i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<MibTcp6RowOwnerPid>(IntPtr.Add(rowPtr, i * rowSize));
                var processId = (int)row.OwningPid;
                yield return new TcpConnectionInfo(
                    "",
                    "IPv6",
                    new IPAddress(row.LocalAddr, row.LocalScopeId),
                    ConvertPort(row.LocalPort),
                    new IPAddress(row.RemoteAddr, row.RemoteScopeId),
                    ConvertPort(row.RemotePort),
                    MapState(row.State),
                    processId,
                    GetProcessName(processId));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int ConvertPort(uint port)
    {
        var bytes = BitConverter.GetBytes(port);
        return (bytes[0] << 8) + bytes[1];
    }

    private static string GetProcessName(int processId)
    {
        if (processId <= 0)
        {
            return "";
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return "unbekannt";
        }
    }

    private static string MapState(uint state)
    {
        return state switch
        {
            1 => "Closed",
            2 => "Listen",
            3 => "SynSent",
            4 => "SynReceived",
            5 => "Established",
            6 => "FinWait1",
            7 => "FinWait2",
            8 => "CloseWait",
            9 => "Closing",
            10 => "LastAck",
            11 => "TimeWait",
            12 => "DeleteTcb",
            _ => "Unknown"
        };
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int tcpTableLength,
        bool sort,
        int ipVersion,
        TcpTableClass tableClass,
        uint reserved);

    private enum TcpTableClass
    {
        OwnerPidAll = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddr;
        public uint LocalPort;
        public uint RemoteAddr;
        public uint RemotePort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcp6RowOwnerPid
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] LocalAddr;

        public uint LocalScopeId;
        public uint LocalPort;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] RemoteAddr;

        public uint RemoteScopeId;
        public uint RemotePort;
        public uint State;
        public uint OwningPid;
    }
}

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
