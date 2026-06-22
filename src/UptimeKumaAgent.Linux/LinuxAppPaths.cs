using UptimeKumaTrayAgent.Services;

namespace UptimeKumaAgent.Linux;

internal sealed class LinuxAppPaths : IAgentPaths
{
    public const string DefaultConfigPath = "/etc/uptime-kuma-agent/config.json";
    public const string DefaultDataDirectory = "/var/lib/uptime-kuma-agent";
    public const string DefaultLogDirectory = "/var/log/uptime-kuma-agent";
    public const string DefaultInstallDirectory = "/opt/uptime-kuma-agent";

    public LinuxAppPaths(string? configPath)
    {
        ConfigPath = string.IsNullOrWhiteSpace(configPath) ? DefaultConfigPath : Path.GetFullPath(configPath);
        if (string.Equals(ConfigPath, DefaultConfigPath, StringComparison.Ordinal))
        {
            DataDirectory = DefaultDataDirectory;
            LogDirectory = DefaultLogDirectory;
        }
        else
        {
            var configDirectory = Path.GetDirectoryName(ConfigPath) ?? Directory.GetCurrentDirectory();
            DataDirectory = Path.Combine(configDirectory, "data");
            LogDirectory = Path.Combine(configDirectory, "logs");
        }
    }

    public string ConfigPath { get; }
    public string DataDirectory { get; }
    public string LogDirectory { get; }
}
