using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Utils;

namespace UptimeKumaTrayAgent.Services;

public sealed class KumaPushService
{
    private readonly Logger _logger;

    public KumaPushService(Logger logger)
    {
        _logger = logger;
    }

    public async Task<PushResult> PushAsync(string pushUrl, MonitorCheckResult checkResult, int timeoutMs, CancellationToken cancellationToken)
    {
        if (!BuildPushUri(pushUrl, checkResult, out var pushUri, out var error))
        {
            return new PushResult { Success = false, ErrorCategory = "Push-URL ungültig", Message = error };
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Clamp(timeoutMs, 500, 120000)));

        try
        {
            using var client = new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan
            };

            using var response = await client.GetAsync(pushUri, timeout.Token).ConfigureAwait(false);
            var statusCode = (int)response.StatusCode;
            _logger.Debug($"Uptime Kuma Push {UrlMasker.MaskPushUrl(pushUrl)} HTTP {statusCode}");

            if (!response.IsSuccessStatusCode)
            {
                return new PushResult
                {
                    Success = false,
                    HttpStatusCode = statusCode,
                    ErrorCategory = CategorizeHttpStatus(response.StatusCode),
                    Message = $"HTTP {statusCode}"
                };
            }

            return new PushResult
            {
                Success = true,
                HttpStatusCode = statusCode,
                Message = $"HTTP {statusCode}"
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new PushResult { Success = false, ErrorCategory = "HTTP-Timeout", Message = "Timeout beim Push" };
        }
        catch (HttpRequestException ex)
        {
            return new PushResult
            {
                Success = false,
                ErrorCategory = CategorizeHttpException(ex),
                Message = ex.Message
            };
        }
        catch (Exception ex)
        {
            return new PushResult
            {
                Success = false,
                ErrorCategory = "unbekannter HTTP-Fehler",
                Message = ex.Message
            };
        }
    }

    public static bool BuildPushUri(string pushUrl, MonitorCheckResult checkResult, out Uri? uri, out string error)
    {
        uri = null;
        error = "";

        if (string.IsNullOrWhiteSpace(pushUrl))
        {
            error = "Push-URL fehlt";
            return false;
        }

        if (!Uri.TryCreate(pushUrl.Trim(), UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            error = "Push-URL ist keine gültige HTTP/HTTPS-Adresse";
            return false;
        }

        var queryParts = ParseQueryPreservingCustomParameters(baseUri.Query);
        queryParts.RemoveAll(part => IsKumaPushParameter(part.Name));
        queryParts.Add(("status", checkResult.IsUp ? "up" : "down"));
        queryParts.Add(("msg", checkResult.Message));

        if (checkResult.PingMs.HasValue)
        {
            queryParts.Add(("ping", checkResult.PingMs.Value.ToString()));
        }

        var builder = new UriBuilder(baseUri)
        {
            Query = string.Join("&", queryParts.Select(part =>
                Uri.EscapeDataString(part.Name) + "=" + Uri.EscapeDataString(part.Value)))
        };

        uri = builder.Uri;
        return true;
    }

    private static List<(string Name, string Value)> ParseQueryPreservingCustomParameters(string query)
    {
        var parts = new List<(string Name, string Value)>();
        var trimmed = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return parts;
        }

        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var rawName = separator >= 0 ? part[..separator] : part;
            var rawValue = separator >= 0 ? part[(separator + 1)..] : "";
            var name = Uri.UnescapeDataString(rawName.Replace("+", " "));
            var value = Uri.UnescapeDataString(rawValue.Replace("+", " "));
            if (!string.IsNullOrWhiteSpace(name))
            {
                parts.Add((name, value));
            }
        }

        return parts;
    }

    private static bool IsKumaPushParameter(string name)
    {
        return string.Equals(name, "status", StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, "msg", StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, "ping", StringComparison.OrdinalIgnoreCase);
    }

    private static string CategorizeHttpStatus(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "HTTP-Fehler 400",
            HttpStatusCode.Unauthorized => "HTTP-Fehler 401",
            HttpStatusCode.Forbidden => "HTTP-Fehler 403",
            HttpStatusCode.NotFound => "HTTP-Fehler 404",
            HttpStatusCode.InternalServerError => "HTTP-Fehler 500",
            _ => "unbekannter HTTP-Fehler"
        };
    }

    private static string CategorizeHttpException(HttpRequestException exception)
    {
        if (ContainsException<AuthenticationException>(exception)
            || exception.Message.Contains("SSL", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("TLS", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("Zertifikat", StringComparison.OrdinalIgnoreCase))
        {
            return "SSL-/TLS-Zertifikatproblem";
        }

        var socket = FindException<SocketException>(exception);
        if (socket is not null)
        {
            return socket.SocketErrorCode switch
            {
                SocketError.HostNotFound or SocketError.NoData => "DNS-Problem bei Push-URL",
                SocketError.TimedOut => "HTTP-Timeout",
                _ => "Push-URL nicht erreichbar"
            };
        }

        return "Push-URL nicht erreichbar";
    }

    private static bool ContainsException<T>(Exception exception) where T : Exception
    {
        return FindException<T>(exception) is not null;
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
