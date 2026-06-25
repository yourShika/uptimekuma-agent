using System.Text.Json.Serialization;
namespace UptimeKumaTrayAgent.Models;

public sealed class AgentConfig
{
    public string Version { get; set; } = AppVersion.Current;

    [JsonPropertyName("global")]
    public GlobalSettings Global { get; set; } = new();

    public UpdateSettings Updates { get; set; } = new();
    public WatchdogSettings Watchdog { get; set; } = new();
    public List<PingCheckConfig> PingChecks { get; set; } = new();
    public List<TcpCheckConfig> TcpChecks { get; set; } = new();
    public List<ServiceCheckConfig> ServiceChecks { get; set; } = new();
    public List<DriveCheckConfig> DriveChecks { get; set; } = new();

    public static AgentConfig CreateDefault()
    {
        return new AgentConfig
        {
            PingChecks = new List<PingCheckConfig>
            {
                new()
                {
                    Name = "Router",
                    Host = "192.168.1.1",
                    IntervalSeconds = 30,
                    TimeoutMs = 3000
                }
            },
            TcpChecks = new List<TcpCheckConfig>
            {
                new()
                {
                    Name = "HTTPS Server",
                    Host = "server01",
                    Port = 443,
                    IntervalSeconds = 30,
                    TimeoutMs = 3000
                }
            },
            ServiceChecks = new List<ServiceCheckConfig>
            {
                new()
                {
                    DisplayName = "Print Spooler",
                    ServiceName = "Spooler",
                    ExpectedStatus = "Running",
                    IntervalSeconds = 30
                }
            },
            DriveChecks = new List<DriveCheckConfig>
            {
                new()
                {
                    Name = "Systemlaufwerk",
                    Path = "C:\\",
                    IntervalSeconds = 60,
                    MinimumFreePercent = 10
                }
            }
        };
    }

    public void Normalize()
    {
        Version = AppVersion.Current;
        Global ??= new GlobalSettings();
        Updates ??= new UpdateSettings();
        Watchdog ??= new WatchdogSettings();
        PingChecks ??= new List<PingCheckConfig>();
        TcpChecks ??= new List<TcpCheckConfig>();
        ServiceChecks ??= new List<ServiceCheckConfig>();
        DriveChecks ??= new List<DriveCheckConfig>();

        Global.Normalize();
        Updates.Normalize();
        Watchdog.Normalize(Global.DefaultIntervalSeconds);

        foreach (var check in PingChecks)
        {
            check.Normalize(Global.DefaultIntervalSeconds, Global.PingTimeoutMs);
        }

        foreach (var check in TcpChecks)
        {
            check.Normalize(Global.DefaultIntervalSeconds, Global.TcpTimeoutMs);
        }

        foreach (var check in ServiceChecks)
        {
            check.Normalize(Global.DefaultIntervalSeconds);
        }

        foreach (var check in DriveChecks)
        {
            check.Normalize(Global.DefaultIntervalSeconds);
        }
    }
}

public sealed class UpdateSettings
{
    public const string DefaultRepository = "yourShika/uptimekuma-agent";

    public string Repository { get; set; } = DefaultRepository;
    public bool IncludePrereleases { get; set; }

    public void Normalize()
    {
        Repository = NormalizeRepository(Repository);
    }

    public static string NormalizeRepository(string? repository)
    {
        if (string.IsNullOrWhiteSpace(repository))
        {
            return DefaultRepository;
        }

        var value = repository.Trim().TrimEnd('/');
        const string githubPrefix = "https://github.com/";
        if (value.StartsWith(githubPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[githubPrefix.Length..].Trim('/');
        }

        if (value.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^4];
        }

        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IsSafeGitHubName(parts[0]) || !IsSafeGitHubName(parts[1]))
        {
            return DefaultRepository;
        }

        return parts[0] + "/" + parts[1];
    }

    private static bool IsSafeGitHubName(string value)
    {
        return value.Length > 0
            && !value.Contains("..", StringComparison.Ordinal)
            && value.All(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
    }
}

public sealed class GlobalSettings
{
    public int DefaultIntervalSeconds { get; set; } = 30;
    public int HttpTimeoutMs { get; set; } = 5000;
    public int TcpTimeoutMs { get; set; } = 3000;
    public int PingTimeoutMs { get; set; } = 3000;
    public bool SendMachineInfo { get; set; } = true;
    public bool MaskPushUrls { get; set; } = true;
    public string LogLevel { get; set; } = "Info";
    public string Theme { get; set; } = "Light";
    public string Language { get; set; } = "System";
    public bool StartMinimized { get; set; } = true;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool Autostart { get; set; }
    public bool MonitoringAutoStart { get; set; } = true;

    public void Normalize()
    {
        DefaultIntervalSeconds = Math.Clamp(DefaultIntervalSeconds, 5, 86400);
        HttpTimeoutMs = Math.Clamp(HttpTimeoutMs, 500, 120000);
        TcpTimeoutMs = Math.Clamp(TcpTimeoutMs, 500, 120000);
        PingTimeoutMs = Math.Clamp(PingTimeoutMs, 500, 120000);
        LogLevel = LogLevelKinds.Normalize(LogLevel);
        Theme = ThemeModeNames.Normalize(Theme);
        Language = AppLanguages.Normalize(Language);
    }
}

public static class AppLanguages
{
    public const string System = "System";
    public const string English = "en";
    public const string German = "de";
    public const string Polish = "pl";

    public static readonly string[] All = { System, English, German, Polish };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return System;
        }

        var normalized = value.Trim();
        return normalized.ToLowerInvariant() switch
        {
            "system" or "auto" or "windows" => System,
            "en" or "eng" or "english" => English,
            "de" or "deu" or "ger" or "german" or "deutsch" => German,
            "pl" or "pol" or "polish" or "polski" => Polish,
            _ => All.FirstOrDefault(language => string.Equals(language, normalized, StringComparison.OrdinalIgnoreCase)) ?? System
        };
    }
}

public static class ThemeModeNames
{
    public static readonly string[] All = { "Light", "Dark" };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Light";
        }

        return All.FirstOrDefault(mode => string.Equals(mode, value.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Light";
    }
}

public sealed class WatchdogSettings
{
    public bool Enabled { get; set; }
    public string PushUrl { get; set; } = "";
    public int IntervalSeconds { get; set; } = 60;
    public int MaxSecondsWithoutSuccessfulCheck { get; set; } = 300;
    public bool SendMachineInfo { get; set; } = true;

    public void Normalize(int defaultIntervalSeconds)
    {
        PushUrl = PushUrl?.Trim() ?? "";
        IntervalSeconds = Math.Clamp(IntervalSeconds <= 0 ? defaultIntervalSeconds : IntervalSeconds, 5, 86400);
        MaxSecondsWithoutSuccessfulCheck = Math.Clamp(MaxSecondsWithoutSuccessfulCheck, 30, 604800);
    }
}

public static class AppVersion
{
    public const string Current = "1.0.9";
}
