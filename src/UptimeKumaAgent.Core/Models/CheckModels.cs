namespace UptimeKumaTrayAgent.Models;

public sealed class PingCheckConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public string PushUrl { get; set; } = "";
    public int IntervalSeconds { get; set; } = 30;
    public int TimeoutMs { get; set; } = 3000;
    public bool SendMachineInfo { get; set; } = true;
    public List<string> RestartServicesOnFailure { get; set; } = new();
    public int RestartServicesCooldownSeconds { get; set; } = 300;
    public int RestartServicesDelayAfterBootMinutes { get; set; }
    public int RestartServicesDelayAfterFailureMinutes { get; set; }
    public bool ForceKillRestartServicesOnTimeout { get; set; }
    public string Note { get; set; } = "";

    public void Normalize(int defaultIntervalSeconds, int defaultTimeoutMs)
    {
        Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id.Trim();
        Name = string.IsNullOrWhiteSpace(Name) ? Host : Name.Trim();
        Host = Host?.Trim() ?? "";
        PushUrl = PushUrl?.Trim() ?? "";
        IntervalSeconds = Math.Clamp(IntervalSeconds <= 0 ? defaultIntervalSeconds : IntervalSeconds, 5, 86400);
        TimeoutMs = Math.Clamp(TimeoutMs <= 0 ? defaultTimeoutMs : TimeoutMs, 500, 120000);
        RestartServicesOnFailure = CheckServiceActions.NormalizeServiceNames(RestartServicesOnFailure);
        RestartServicesCooldownSeconds = Math.Clamp(RestartServicesCooldownSeconds <= 0 ? 300 : RestartServicesCooldownSeconds, 30, 86400);
        RestartServicesDelayAfterBootMinutes = Math.Clamp(RestartServicesDelayAfterBootMinutes, 0, 1440);
        RestartServicesDelayAfterFailureMinutes = Math.Clamp(RestartServicesDelayAfterFailureMinutes, 0, 1440);
        Note = Note?.Trim() ?? "";
    }

    public PingCheckConfig Clone()
    {
        var clone = (PingCheckConfig)MemberwiseClone();
        clone.RestartServicesOnFailure = RestartServicesOnFailure.ToList();
        return clone;
    }
}

public sealed class TcpCheckConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 443;
    public string PushUrl { get; set; } = "";
    public int IntervalSeconds { get; set; } = 30;
    public int TimeoutMs { get; set; } = 3000;
    public bool SendMachineInfo { get; set; } = true;
    public List<string> RestartServicesOnFailure { get; set; } = new();
    public int RestartServicesCooldownSeconds { get; set; } = 300;
    public int RestartServicesDelayAfterBootMinutes { get; set; }
    public int RestartServicesDelayAfterFailureMinutes { get; set; }
    public bool ForceKillRestartServicesOnTimeout { get; set; }
    public bool LogTcpConnections { get; set; }
    public string TcpConnectionLogDirection { get; set; } = TcpConnectionLogDirections.Both;
    public string Note { get; set; } = "";

    public void Normalize(int defaultIntervalSeconds, int defaultTimeoutMs)
    {
        Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id.Trim();
        Name = string.IsNullOrWhiteSpace(Name) ? $"{Host}:{Port}" : Name.Trim();
        Host = Host?.Trim() ?? "";
        Port = Math.Clamp(Port, 1, 65535);
        PushUrl = PushUrl?.Trim() ?? "";
        IntervalSeconds = Math.Clamp(IntervalSeconds <= 0 ? defaultIntervalSeconds : IntervalSeconds, 5, 86400);
        TimeoutMs = Math.Clamp(TimeoutMs <= 0 ? defaultTimeoutMs : TimeoutMs, 500, 120000);
        RestartServicesOnFailure = CheckServiceActions.NormalizeServiceNames(RestartServicesOnFailure);
        RestartServicesCooldownSeconds = Math.Clamp(RestartServicesCooldownSeconds <= 0 ? 300 : RestartServicesCooldownSeconds, 30, 86400);
        RestartServicesDelayAfterBootMinutes = Math.Clamp(RestartServicesDelayAfterBootMinutes, 0, 1440);
        RestartServicesDelayAfterFailureMinutes = Math.Clamp(RestartServicesDelayAfterFailureMinutes, 0, 1440);
        TcpConnectionLogDirection = TcpConnectionLogDirections.Normalize(TcpConnectionLogDirection);
        Note = Note?.Trim() ?? "";
    }

    public TcpCheckConfig Clone()
    {
        var clone = (TcpCheckConfig)MemberwiseClone();
        clone.RestartServicesOnFailure = RestartServicesOnFailure.ToList();
        return clone;
    }
}

public sealed class ServiceCheckConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string DisplayName { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public string ExpectedStatus { get; set; } = "Running";
    public string PushUrl { get; set; } = "";
    public int IntervalSeconds { get; set; } = 30;
    public bool RestartIfStopped { get; set; }
    public bool SendMachineInfo { get; set; } = true;
    public List<string> RestartServicesOnFailure { get; set; } = new();
    public int RestartServicesCooldownSeconds { get; set; } = 300;
    public int RestartServicesDelayAfterBootMinutes { get; set; }
    public int RestartServicesDelayAfterFailureMinutes { get; set; }
    public bool ForceKillRestartServicesOnTimeout { get; set; }
    public int RestartIfStoppedDelayAfterBootMinutes { get; set; }
    public int RestartIfStoppedDelayAfterFailureMinutes { get; set; }
    public string Note { get; set; } = "";

    public void Normalize(int defaultIntervalSeconds)
    {
        Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id.Trim();
        ServiceName = ServiceName?.Trim() ?? "";
        DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? ServiceName : DisplayName.Trim();
        ExpectedStatus = ServiceStates.Normalize(ExpectedStatus);
        PushUrl = PushUrl?.Trim() ?? "";
        IntervalSeconds = Math.Clamp(IntervalSeconds <= 0 ? defaultIntervalSeconds : IntervalSeconds, 5, 86400);
        RestartServicesOnFailure = CheckServiceActions.NormalizeServiceNames(RestartServicesOnFailure);
        RestartServicesCooldownSeconds = Math.Clamp(RestartServicesCooldownSeconds <= 0 ? 300 : RestartServicesCooldownSeconds, 30, 86400);
        RestartServicesDelayAfterBootMinutes = Math.Clamp(RestartServicesDelayAfterBootMinutes, 0, 1440);
        RestartServicesDelayAfterFailureMinutes = Math.Clamp(RestartServicesDelayAfterFailureMinutes, 0, 1440);
        RestartIfStoppedDelayAfterBootMinutes = Math.Clamp(RestartIfStoppedDelayAfterBootMinutes, 0, 1440);
        RestartIfStoppedDelayAfterFailureMinutes = Math.Clamp(RestartIfStoppedDelayAfterFailureMinutes, 0, 1440);
        Note = Note?.Trim() ?? "";
    }

    public ServiceCheckConfig Clone()
    {
        var clone = (ServiceCheckConfig)MemberwiseClone();
        clone.RestartServicesOnFailure = RestartServicesOnFailure.ToList();
        return clone;
    }

}

public sealed class DriveCheckConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string PushUrl { get; set; } = "";
    public int IntervalSeconds { get; set; } = 60;
    public int MinimumFreePercent { get; set; } = 10;
    public decimal MinimumFreeGb { get; set; }
    public bool ReconnectIfUnavailable { get; set; }
    public string ReconnectPath { get; set; } = "";
    public bool LogDetails { get; set; } = true;
    public bool SendMachineInfo { get; set; } = true;
    public string Note { get; set; } = "";

    public void Normalize(int defaultIntervalSeconds)
    {
        Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id.Trim();
        Path = NormalizePath(Path);
        Name = string.IsNullOrWhiteSpace(Name) ? Path : Name.Trim();
        PushUrl = PushUrl?.Trim() ?? "";
        IntervalSeconds = Math.Clamp(IntervalSeconds <= 0 ? defaultIntervalSeconds : IntervalSeconds, 5, 86400);
        MinimumFreePercent = Math.Clamp(MinimumFreePercent, 0, 100);
        MinimumFreeGb = Math.Clamp(MinimumFreeGb, 0, 1024 * 1024);
        ReconnectPath = ReconnectPath?.Trim() ?? "";
        Note = Note?.Trim() ?? "";
    }

    public DriveCheckConfig Clone()
    {
        return (DriveCheckConfig)MemberwiseClone();
    }

    public static string NormalizePath(string? path)
    {
        var normalized = path?.Trim() ?? "";
        if (normalized.Length == 2 && normalized[1] == ':')
        {
            return normalized + "\\";
        }

        return normalized;
    }
}

public static class CheckServiceActions
{
    public static List<string> NormalizeServiceNames(IEnumerable<string>? serviceNames)
    {
        return serviceNames?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList() ?? new List<string>();
    }
}

public static class TcpConnectionLogDirections
{
    public const string Both = "Ein- und ausgehend";
    public const string Incoming = "Eingehend";
    public const string Outgoing = "Ausgehend";

    public static readonly string[] All = { Both, Incoming, Outgoing };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Both;
        }

        var normalized = value.Trim();
        if (string.Equals(normalized, "both", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "inout", StringComparison.OrdinalIgnoreCase))
        {
            return Both;
        }

        if (string.Equals(normalized, "incoming", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "in", StringComparison.OrdinalIgnoreCase))
        {
            return Incoming;
        }

        if (string.Equals(normalized, "outgoing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "out", StringComparison.OrdinalIgnoreCase))
        {
            return Outgoing;
        }

        return All.FirstOrDefault(direction => string.Equals(direction, normalized, StringComparison.OrdinalIgnoreCase)) ?? Both;
    }

    public static bool IncludesIncoming(string? value)
    {
        var normalized = Normalize(value);
        return normalized is Both or Incoming;
    }

    public static bool IncludesOutgoing(string? value)
    {
        var normalized = Normalize(value);
        return normalized is Both or Outgoing;
    }
}

public static class ServiceStates
{
    public static readonly string[] All =
    {
        "Running",
        "Stopped",
        "Paused",
        "StartPending",
        "StopPending",
        "ContinuePending",
        "PausePending"
    };

    public static string Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Running";
        }

        return All.FirstOrDefault(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Running";
    }
}

public static class LogLevelKinds
{
    public static readonly string[] All = { "Info", "Warnung", "Fehler", "Debug" };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Info";
        }

        var normalized = value.Trim();
        return All.FirstOrDefault(level => string.Equals(level, normalized, StringComparison.OrdinalIgnoreCase)) ?? "Info";
    }
}
