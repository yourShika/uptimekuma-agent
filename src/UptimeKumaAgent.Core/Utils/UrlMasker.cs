using System.Text.RegularExpressions;

namespace UptimeKumaTrayAgent.Utils;

public static partial class UrlMasker
{
    public static string MaskPushUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "";
        }

        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return "ungültige-url-****";
        }

        var authority = uri.GetLeftPart(UriPartial.Authority);
        var path = uri.AbsolutePath.TrimEnd('/');
        var pushIndex = path.IndexOf("/api/push/", StringComparison.OrdinalIgnoreCase);
        if (pushIndex >= 0)
        {
            return authority + path[..(pushIndex + "/api/push/".Length)] + "****";
        }

        var firstSegment = path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstSegment)
            ? authority + "/****"
            : authority + "/" + firstSegment + "/****";
    }

    public static string MaskPotentialUrls(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return "";
        }

        return HttpUrlRegex().Replace(message, match => MaskPushUrl(match.Value));
    }

    [GeneratedRegex(@"https?://[^\s""']+", RegexOptions.IgnoreCase)]
    private static partial Regex HttpUrlRegex();
}
