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

public sealed class WindowsServiceInfo
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
