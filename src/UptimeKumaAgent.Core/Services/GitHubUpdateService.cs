using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using UptimeKumaTrayAgent.Models;

namespace UptimeKumaTrayAgent.Services;

public sealed class GitHubUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public GitHubUpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        UpdateSettings settings,
        string currentVersion,
        CancellationToken cancellationToken)
    {
        settings ??= new UpdateSettings();
        settings.Normalize();
        currentVersion = string.IsNullOrWhiteSpace(currentVersion) ? AppVersion.Current : currentVersion.Trim();
        var platform = DetectCurrentPlatform();

        try
        {
            var releases = await LoadReleasesAsync(settings.Repository, cancellationToken).ConfigureAwait(false);
            var candidates = releases
                .Where(release => !release.Draft && (settings.IncludePrereleases || !release.Prerelease))
                .Select(ToReleaseInfo)
                .Where(release => !string.IsNullOrWhiteSpace(release.TagName))
                .OrderByDescending(release => GetComparableVersion(release.TagName))
                .ThenByDescending(release => release.PublishedAt ?? DateTimeOffset.MinValue)
                .ToList();

            if (candidates.Count == 0)
            {
                return new UpdateCheckResult
                {
                    Success = true,
                    CurrentVersion = currentVersion,
                    Platform = platform,
                    Message = "Keine GitHub Releases gefunden."
                };
            }

            var latest = candidates[0];
            var updateAvailable = IsNewerVersion(latest.TagName, currentVersion);
            if (!updateAvailable)
            {
                return new UpdateCheckResult
                {
                    Success = true,
                    CurrentVersion = currentVersion,
                    Platform = platform,
                    Release = latest,
                    Message = $"Installierte Version {currentVersion} ist aktuell."
                };
            }

            var asset = SelectAsset(latest, platform);
            var message = asset is null
                ? $"Neue Version {latest.Version} ist verfügbar, aber kein passendes Paket für {platform} wurde gefunden."
                : $"Neue Version {latest.Version} ist verfügbar.";

            return new UpdateCheckResult
            {
                Success = true,
                UpdateAvailable = true,
                CurrentVersion = currentVersion,
                Platform = platform,
                Release = latest,
                Asset = asset,
                Message = message
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return Failed(currentVersion, platform, "GitHubReleaseRequest", "GitHub Releases konnten nicht abgefragt werden: " + ex.Message);
        }
        catch (JsonException ex)
        {
            return Failed(currentVersion, platform, "GitHubReleaseJson", "GitHub Release-Antwort konnte nicht gelesen werden: " + ex.Message);
        }
    }

    public async Task<string> DownloadAssetAsync(
        GitHubReleaseAsset asset,
        string downloadDirectory,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl)
            || !Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Download-URL des GitHub Release-Assets ist ungültig.");
        }

        Directory.CreateDirectory(downloadDirectory);
        var destinationPath = Path.Combine(downloadDirectory, ToSafeFileName(asset.Name));

        using var request = CreateGitHubRequest(uri);
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await ReadErrorDetailAsync(response, cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"Download fehlgeschlagen: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {detail}");
        }

        var totalBytes = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = File.Create(destinationPath);

        var buffer = new byte[81920];
        long received = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;
            progress?.Report(new DownloadProgress { BytesReceived = received, TotalBytes = totalBytes });
        }

        return destinationPath;
    }

    public static bool IsNewerVersion(string? candidateVersion, string? currentVersion)
    {
        if (!TryParseVersion(candidateVersion, out var candidate))
        {
            return false;
        }

        if (!TryParseVersion(currentVersion, out var current))
        {
            return true;
        }

        return candidate.CompareTo(current) > 0;
    }

    public static GitHubReleaseAsset? SelectAsset(GitHubReleaseInfo release, AgentUpdatePlatform platform)
    {
        var assets = release.Assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Name) && !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            .ToList();

        return platform switch
        {
            AgentUpdatePlatform.WindowsX64 => FindAsset(assets, name => IsMsi(name) && ContainsToken(name, "x64") && !ContainsToken(name, "arm64")),
            AgentUpdatePlatform.WindowsX86 => FindAsset(assets, name => IsMsi(name) && (ContainsToken(name, "x86") || ContainsToken(name, "win-x86"))),
            AgentUpdatePlatform.LinuxX64 => FindAsset(assets, name => IsTarGz(name) && ContainsToken(name, "linux-x64")),
            AgentUpdatePlatform.LinuxArm64 => FindAsset(assets, name => IsTarGz(name) && ContainsToken(name, "linux-arm64")),
            _ => null
        };
    }

    public static AgentUpdatePlatform DetectCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => AgentUpdatePlatform.WindowsX64,
                Architecture.X86 => AgentUpdatePlatform.WindowsX86,
                _ => AgentUpdatePlatform.Unsupported
            };
        }

        if (OperatingSystem.IsLinux())
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => AgentUpdatePlatform.LinuxX64,
                Architecture.Arm64 => AgentUpdatePlatform.LinuxArm64,
                _ => AgentUpdatePlatform.Unsupported
            };
        }

        return AgentUpdatePlatform.Unsupported;
    }

    private async Task<List<GitHubReleaseDto>> LoadReleasesAsync(string repository, CancellationToken cancellationToken)
    {
        var uri = new Uri($"https://api.github.com/repos/{repository}/releases");
        using var request = CreateGitHubRequest(uri);
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new List<GitHubReleaseDto>();
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await ReadErrorDetailAsync(response, cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {detail}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer
            .DeserializeAsync<List<GitHubReleaseDto>>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false) ?? new List<GitHubReleaseDto>();
    }

    private static HttpRequestMessage CreateGitHubRequest(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", "UptimeKumaTrayAgent/" + AppVersion.Current);
        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private static async Task<string> ReadErrorDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return body.Length > 500 ? body[..500] : body;
        }
        catch
        {
            return "";
        }
    }

    private static UpdateCheckResult Failed(string currentVersion, AgentUpdatePlatform platform, string category, string message)
    {
        return new UpdateCheckResult
        {
            Success = false,
            CurrentVersion = currentVersion,
            Platform = platform,
            ErrorCategory = category,
            Message = message
        };
    }

    private static GitHubReleaseInfo ToReleaseInfo(GitHubReleaseDto dto)
    {
        var assets = dto.Assets?
            .Select(asset => new GitHubReleaseAsset
            {
                Name = asset.Name ?? "",
                BrowserDownloadUrl = asset.BrowserDownloadUrl ?? "",
                Size = asset.Size
            })
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Name))
            .ToList() ?? new List<GitHubReleaseAsset>();

        return new GitHubReleaseInfo
        {
            TagName = dto.TagName ?? "",
            Version = NormalizeVersionText(dto.TagName),
            Name = dto.Name ?? "",
            Body = dto.Body ?? "",
            HtmlUrl = dto.HtmlUrl ?? "",
            Prerelease = dto.Prerelease,
            PublishedAt = dto.PublishedAt,
            Assets = assets
        };
    }

    private static GitHubReleaseAsset? FindAsset(
        IEnumerable<GitHubReleaseAsset> assets,
        Func<string, bool> predicate)
    {
        return assets.FirstOrDefault(asset => predicate(asset.Name));
    }

    private static bool IsMsi(string name)
    {
        return name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTarGz(string name)
    {
        return name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsToken(string name, string token)
    {
        return name.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static Version GetComparableVersion(string tagName)
    {
        return TryParseVersion(tagName, out var version) ? version : new Version(0, 0, 0, 0);
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        var text = NormalizeVersionText(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var suffixIndex = text.IndexOfAny(new[] { '-', '+' });
        if (suffixIndex >= 0)
        {
            text = text[..suffixIndex];
        }

        var parts = text.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is 0 or > 4)
        {
            return false;
        }

        var numbers = new List<int>();
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var number) || number < 0)
            {
                return false;
            }

            numbers.Add(number);
        }

        while (numbers.Count < 4)
        {
            numbers.Add(0);
        }

        version = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
        return true;
    }

    private static string NormalizeVersionText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var text = value.Trim();
        return text.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? text[1..] : text;
    }

    private static string ToSafeFileName(string name)
    {
        var fileName = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "uptime-kuma-agent-update";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '_');
        }

        return fileName;
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("body")]
        public string? Body { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAssetDto>? Assets { get; init; }
    }

    private sealed class GitHubReleaseAssetDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }

        [JsonPropertyName("size")]
        public long Size { get; init; }
    }
}
