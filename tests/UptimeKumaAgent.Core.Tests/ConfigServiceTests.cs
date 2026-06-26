using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Services;
using Xunit;

namespace UptimeKumaAgent.Core.Tests;

public sealed class ConfigServiceTests
{
    [Fact]
    public void LoadOrThrow_RoundTripsConfigAndNormalizesVersion()
    {
        using var temp = new TempAgentPaths();
        var service = new ConfigService(temp);
        var config = AgentConfig.CreateDefault();
        config.Version = "0.0.1";
        config.PingChecks[0].RestartServicesDelayAfterBootMinutes = 3000;
        config.PingChecks[0].RestartServicesDelayAfterFailureMinutes = -1;

        service.Save(config);
        var loaded = service.LoadOrThrow(createIfMissing: false);

        Assert.Equal(AppVersion.Current, loaded.Version);
        Assert.Equal(1440, loaded.PingChecks[0].RestartServicesDelayAfterBootMinutes);
        Assert.Equal(0, loaded.PingChecks[0].RestartServicesDelayAfterFailureMinutes);
    }

    [Fact]
    public void LoadOrThrow_ThrowsWhenMissingAndCreateDisabled()
    {
        using var temp = new TempAgentPaths();
        var service = new ConfigService(temp);

        Assert.Throws<FileNotFoundException>(() => service.LoadOrThrow(createIfMissing: false));
    }

    private sealed class TempAgentPaths : IAgentPaths, IDisposable
    {
        public TempAgentPaths()
        {
            DataDirectory = Path.Combine(Path.GetTempPath(), "uptime-kuma-agent-tests", Guid.NewGuid().ToString("N"));
            LogDirectory = Path.Combine(DataDirectory, "logs");
            ConfigPath = Path.Combine(DataDirectory, "config.json");
        }

        public string ConfigPath { get; }
        public string DataDirectory { get; }
        public string LogDirectory { get; }

        public void Dispose()
        {
            if (Directory.Exists(DataDirectory))
            {
                Directory.Delete(DataDirectory, recursive: true);
            }
        }
    }
}
