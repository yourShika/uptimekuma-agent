using System.Net;
using System.Text.RegularExpressions;

namespace UptimeKumaTrayAgent.Utils;

public static partial class ValidationUtils
{
    public static bool IsValidHttpUrl(string? value, bool allowEmpty)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return allowEmpty;
        }

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
               && (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
                   || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
               && !string.IsNullOrWhiteSpace(uri.Host);
    }

    public static bool IsValidHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var host = value.Trim();
        if (IPAddress.TryParse(host, out _))
        {
            return true;
        }

        return HostRegex().IsMatch(host) && host.Length <= 253;
    }

    public static bool IsValidPort(int port)
    {
        return port is >= 1 and <= 65535;
    }

    public static bool IsValidInterval(int seconds)
    {
        return seconds is >= 5 and <= 86400;
    }

    public static bool IsValidTimeout(int milliseconds)
    {
        return milliseconds is >= 500 and <= 120000;
    }

    public static string Require(bool condition, string message)
    {
        return condition ? "" : message;
    }

    [GeneratedRegex(@"^([a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)*[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?$")]
    private static partial Regex HostRegex();
}
