using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Utils;

namespace UptimeKumaTrayAgent.Services;

public sealed class MachineInfoProvider
{
    public string BuildSummary()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var user = Environment.UserDomainName + "\\" + Environment.UserName;
        var os = Environment.OSVersion.VersionString;
        var domain = IPGlobalProperties.GetIPGlobalProperties().DomainName;
        var domainText = string.IsNullOrWhiteSpace(domain) ? Environment.UserDomainName : domain;
        var addresses = GetLocalIpAddresses();

        return $"Host={Environment.MachineName} | User={user} | OS={os} | Domain={domainText} | IPs={addresses} | Uptime={TimeFormatter.FormatDuration(uptime)} | Agent={AppVersion.Current}";
    }

    private static string GetLocalIpAddresses()
    {
        try
        {
            var addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .Where(ip => ip.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                .Where(ip => !IPAddress.IsLoopback(ip))
                .Select(ip => ip.ToString())
                .Take(8)
                .ToArray();

            return addresses.Length == 0 ? "n/a" : string.Join(",", addresses);
        }
        catch
        {
            return "n/a";
        }
    }
}
