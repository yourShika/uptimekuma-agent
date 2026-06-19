using UptimeKumaTrayAgent.Utils;

namespace UptimeKumaTrayAgent.Services;

public enum AgentLogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

public sealed class Logger
{
    private readonly object _lock = new();
    private readonly string _logDirectory;
    private AgentLogLevel _minimum = AgentLogLevel.Info;

    public Logger(AppPaths paths)
    {
        _logDirectory = paths.LogDirectory;
        Directory.CreateDirectory(_logDirectory);
        DeleteOldLogs(30);
    }

    public string LogDirectory => _logDirectory;

    public void SetLevel(string? level)
    {
        _minimum = Parse(level);
    }

    public void Debug(string message) => Write(AgentLogLevel.Debug, message);
    public void Info(string message) => Write(AgentLogLevel.Info, message);
    public void Warning(string message) => Write(AgentLogLevel.Warning, message);
    public void Error(string message, Exception? exception = null) => Write(AgentLogLevel.Error, message, exception);

    public void Write(AgentLogLevel level, string message, Exception? exception = null)
    {
        if (level < _minimum)
        {
            return;
        }

        var safeMessage = UrlMasker.MaskPotentialUrls(message);
        var details = exception is null ? "" : " | " + UrlMasker.MaskPotentialUrls(exception.ToString());
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{ToDisplay(level)}] {safeMessage}{details}{Environment.NewLine}";

        lock (_lock)
        {
            Directory.CreateDirectory(_logDirectory);
            File.AppendAllText(GetLogPath(), line);
        }
    }

    public string GetLogPath()
    {
        return Path.Combine(_logDirectory, $"agent-{DateTimeOffset.Now:yyyy-MM-dd}.log");
    }

    public string ReadRecentLines(int maxLines = 200)
    {
        try
        {
            var path = GetLogPath();
            if (!File.Exists(path))
            {
                return "";
            }

            var lines = File.ReadLines(path).TakeLast(maxLines);
            return string.Join(Environment.NewLine, lines);
        }
        catch (Exception ex)
        {
            return "Logs konnten nicht gelesen werden: " + ex.Message;
        }
    }

    public void DeleteLogs()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(_logDirectory, "*.log"))
            {
                File.Delete(file);
            }
        }
        catch
        {
            // Best effort cleanup during uninstall.
        }
    }

    private void DeleteOldLogs(int days)
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                return;
            }

            var cutoff = DateTime.Now.AddDays(-days);
            foreach (var file in Directory.GetFiles(_logDirectory, "agent-*.log"))
            {
                var info = new FileInfo(file);
                if (info.LastWriteTime < cutoff)
                {
                    info.Delete();
                }
            }
        }
        catch
        {
            // Log rotation must never prevent the agent from starting.
        }
    }

    private static AgentLogLevel Parse(string? level)
    {
        return level?.Trim().ToLowerInvariant() switch
        {
            "debug" => AgentLogLevel.Debug,
            "warnung" => AgentLogLevel.Warning,
            "warning" => AgentLogLevel.Warning,
            "fehler" => AgentLogLevel.Error,
            "error" => AgentLogLevel.Error,
            _ => AgentLogLevel.Info
        };
    }

    private static string ToDisplay(AgentLogLevel level)
    {
        return level switch
        {
            AgentLogLevel.Debug => "Debug",
            AgentLogLevel.Warning => "Warnung",
            AgentLogLevel.Error => "Fehler",
            _ => "Info"
        };
    }
}
