namespace UptimeKumaTrayAgent.Models;

public enum CheckType
{
    Ping,
    Tcp,
    Service,
    Drive,
    Watchdog
}

public enum CheckState
{
    Unknown,
    Up,
    Down,
    Warning,
    Disabled
}

public sealed class CheckRuntimeStatus
{
    public string Id { get; set; } = "";
    public CheckType Type { get; set; }
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public bool Enabled { get; set; }
    public CheckState State { get; set; } = CheckState.Unknown;
    public long? LastResponseMs { get; set; }
    public DateTimeOffset? LastRun { get; set; }
    public DateTimeOffset? LastSuccessfulCheck { get; set; }
    public string LastError { get; set; } = "";
    public DateTimeOffset? NextRun { get; set; }
    public DateTimeOffset? LastPushAt { get; set; }
    public string LastPushHttpStatus { get; set; } = "";
    public bool IsRunning { get; set; }
    public string LastMessage { get; set; } = "";

    public CheckRuntimeStatus Clone()
    {
        return (CheckRuntimeStatus)MemberwiseClone();
    }
}

public sealed class MonitorCheckResult
{
    public bool IsUp { get; init; }
    public string Message { get; init; } = "";
    public long? PingMs { get; init; }
    public string ErrorCategory { get; init; } = "";
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.Now;

    public static MonitorCheckResult Up(string message, long? pingMs = null)
    {
        return new MonitorCheckResult { IsUp = true, Message = message, PingMs = pingMs };
    }

    public static MonitorCheckResult Down(string message, string category, long? pingMs = null)
    {
        return new MonitorCheckResult { IsUp = false, Message = message, ErrorCategory = category, PingMs = pingMs };
    }
}

public sealed class PushResult
{
    public bool Success { get; init; }
    public int? HttpStatusCode { get; init; }
    public string ErrorCategory { get; init; } = "";
    public string Message { get; init; } = "";
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.Now;
}

public sealed class ServiceInfo
{
    public string ServiceName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Status { get; init; } = "";
    public bool CanStop { get; init; }
}

public sealed class ServiceOperationResult
{
    public bool Success { get; init; }
    public string Status { get; init; } = "";
    public string ErrorCategory { get; init; } = "";
    public string Message { get; init; } = "";
}

public enum AgentUpdatePlatform
{
    Unsupported,
    WindowsX64,
    WindowsX86,
    LinuxX64,
    LinuxArm64
}

public sealed class GitHubReleaseAsset
{
    public string Name { get; init; } = "";
    public string BrowserDownloadUrl { get; init; } = "";
    public long Size { get; init; }
}

public sealed class GitHubReleaseInfo
{
    public string TagName { get; init; } = "";
    public string Version { get; init; } = "";
    public string Name { get; init; } = "";
    public string Body { get; init; } = "";
    public string HtmlUrl { get; init; } = "";
    public bool Prerelease { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public IReadOnlyList<GitHubReleaseAsset> Assets { get; init; } = Array.Empty<GitHubReleaseAsset>();
}

public sealed class UpdateCheckResult
{
    public bool Success { get; init; }
    public bool UpdateAvailable { get; init; }
    public string CurrentVersion { get; init; } = AppVersion.Current;
    public AgentUpdatePlatform Platform { get; init; } = AgentUpdatePlatform.Unsupported;
    public GitHubReleaseInfo? Release { get; init; }
    public GitHubReleaseAsset? Asset { get; init; }
    public string Message { get; init; } = "";
    public string ErrorCategory { get; init; } = "";
}

public sealed class DownloadProgress
{
    public long BytesReceived { get; init; }
    public long? TotalBytes { get; init; }

    public double? Percent => TotalBytes is > 0
        ? Math.Round(BytesReceived * 100d / TotalBytes.Value, 1)
        : null;
}
