using System.Text.Json;

namespace UptimeKumaTrayAgent.Services;

public sealed class AppPaths
{
    public const string AppFolderName = "UptimeKumaTrayAgent";

    public string DataDirectory { get; }
    public string ConfigPath => Path.Combine(DataDirectory, "config.json");
    public string LogDirectory => Path.Combine(DataDirectory, "Logs");

    public AppPaths()
    {
        DataDirectory = ResolveDataDirectory();
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
        MigrateLegacyUserConfigIfNeeded(DataDirectory);
    }

    private static string ResolveDataDirectory()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var preferred = Path.Combine(programData, AppFolderName);
        var legacy = GetCurrentUserLegacyDirectory();

        if (File.Exists(Path.Combine(preferred, "config.json")) && CanUseDirectory(preferred))
        {
            return preferred;
        }

        if (File.Exists(Path.Combine(legacy, "config.json")) && !CanUseDirectory(preferred))
        {
            return legacy;
        }

        return CanUseDirectory(preferred) ? preferred : legacy;
    }

    private static void MigrateLegacyUserConfigIfNeeded(string targetDataDirectory)
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var preferred = Path.Combine(programData, AppFolderName);
        if (!string.Equals(targetDataDirectory, preferred, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var targetConfig = Path.Combine(preferred, "config.json");
        var legacyConfig = FindLegacyConfigCandidates(targetConfig).FirstOrDefault();
        if (legacyConfig is null)
        {
            return;
        }

        Directory.CreateDirectory(preferred);
        if (!File.Exists(targetConfig))
        {
            CopyConfigWithBackup(legacyConfig.FullName, targetConfig, overwrite: false);
            return;
        }

        if (IsFactoryDefaultConfig(targetConfig))
        {
            var backupPath = Path.Combine(preferred, $"config.factory-default-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.json");
            File.Copy(targetConfig, backupPath, overwrite: true);
            CopyConfigWithBackup(legacyConfig.FullName, targetConfig, overwrite: true);
        }
    }

    private static string GetCurrentUserLegacyDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, AppFolderName);
    }

    private static IEnumerable<FileInfo> FindLegacyConfigCandidates(string targetConfigPath)
    {
        var candidates = new List<FileInfo>();
        var currentUserConfig = Path.Combine(GetCurrentUserLegacyDirectory(), "config.json");
        AddCandidate(candidates, currentUserConfig, targetConfigPath);

        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
        var usersRoot = string.IsNullOrWhiteSpace(systemDrive)
            ? @"C:\Users"
            : Path.Combine(systemDrive, "Users");

        try
        {
            if (Directory.Exists(usersRoot))
            {
                foreach (var userDirectory in Directory.EnumerateDirectories(usersRoot))
                {
                    var candidate = Path.Combine(userDirectory, "AppData", "Roaming", AppFolderName, "config.json");
                    AddCandidate(candidates, candidate, targetConfigPath);
                }
            }
        }
        catch
        {
            // Best effort: older per-user configs are a migration convenience.
        }

        return candidates
            .GroupBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(file => file.LastWriteTimeUtc);
    }

    private static void AddCandidate(List<FileInfo> candidates, string path, string targetConfigPath)
    {
        try
        {
            if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(targetConfigPath), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var file = new FileInfo(path);
            if (file.Exists && file.Length > 0)
            {
                candidates.Add(file);
            }
        }
        catch
        {
            // Ignore unreadable profiles.
        }
    }

    private static void CopyConfigWithBackup(string sourceConfig, string targetConfig, bool overwrite)
    {
        File.Copy(sourceConfig, targetConfig, overwrite);

        var sourceBackup = sourceConfig + ".bak";
        var targetBackup = targetConfig + ".bak";
        if (File.Exists(sourceBackup) && (overwrite || !File.Exists(targetBackup)))
        {
            File.Copy(sourceBackup, targetBackup, overwrite);
        }
    }

    private static bool IsFactoryDefaultConfig(string configPath)
    {
        try
        {
            using var stream = File.OpenRead(configPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            return HasSingleCheck(root, "pingChecks", "name", "Router", "host", "192.168.1.1")
                   && HasSingleCheck(root, "tcpChecks", "name", "HTTPS Server", "host", "server01", "port", "443")
                   && HasSingleCheck(root, "serviceChecks", "serviceName", "Spooler");
        }
        catch
        {
            return false;
        }
    }

    private static bool HasSingleCheck(JsonElement root, string arrayName, params string[] expectedPairs)
    {
        if (!root.TryGetProperty(arrayName, out var checks)
            || checks.ValueKind != JsonValueKind.Array
            || checks.GetArrayLength() != 1)
        {
            return false;
        }

        var check = checks[0];
        for (var i = 0; i < expectedPairs.Length; i += 2)
        {
            var propertyName = expectedPairs[i];
            var expectedValue = expectedPairs[i + 1];
            if (!check.TryGetProperty(propertyName, out var property))
            {
                return false;
            }

            var actualValue = property.ValueKind == JsonValueKind.Number
                ? property.GetRawText()
                : property.GetString();
            if (!string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanUseDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, ".write-test");
            File.WriteAllText(probe, DateTimeOffset.Now.ToString("O"));
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
