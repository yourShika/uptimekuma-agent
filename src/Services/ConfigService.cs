using System.Text.Json;
using UptimeKumaTrayAgent.Models;

namespace UptimeKumaTrayAgent.Services;

public sealed class ConfigService
{
    private readonly AppPaths _paths;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ConfigService(AppPaths paths)
    {
        _paths = paths;
    }

    public string ConfigPath => _paths.ConfigPath;
    public string DataDirectory => _paths.DataDirectory;
    public string LogDirectory => _paths.LogDirectory;

    public AgentConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var defaults = AgentConfig.CreateDefault();
                defaults.Normalize();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AgentConfig>(json, _jsonOptions) ?? AgentConfig.CreateDefault();
            config.Normalize();
            return config;
        }
        catch
        {
            var fallback = AgentConfig.CreateDefault();
            fallback.Normalize();
            return fallback;
        }
    }

    public void Save(AgentConfig config)
    {
        config.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var tempPath = ConfigPath + ".tmp";
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        File.WriteAllText(tempPath, json);
        if (File.Exists(ConfigPath))
        {
            File.Replace(tempPath, ConfigPath, ConfigPath + ".bak", true);
        }
        else
        {
            File.Move(tempPath, ConfigPath);
        }
    }

    public void DeleteConfig()
    {
        TryDelete(ConfigPath);
        TryDelete(ConfigPath + ".bak");
        TryDelete(ConfigPath + ".tmp");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup during uninstall.
        }
    }
}
