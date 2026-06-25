using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Services;
using Xunit;

namespace UptimeKumaAgent.Core.Tests;

public sealed class GitHubUpdateServiceTests
{
    [Theory]
    [InlineData("v1.0.11", "1.0.10", true)]
    [InlineData("1.0.10", "1.0.10", false)]
    [InlineData("1.0.10-beta.1", "1.0.8", true)]
    [InlineData("not-a-version", "1.0.10", false)]
    public void IsNewerVersion_ComparesReleaseTags(string candidate, string current, bool expected)
    {
        Assert.Equal(expected, GitHubUpdateService.IsNewerVersion(candidate, current));
    }

    [Theory]
    [InlineData("https://github.com/yourShika/uptimekuma-agent.git", "yourShika/uptimekuma-agent")]
    [InlineData("yourShika/uptimekuma-agent", "yourShika/uptimekuma-agent")]
    [InlineData("../bad", "yourShika/uptimekuma-agent")]
    public void UpdateSettings_NormalizesRepository(string input, string expected)
    {
        Assert.Equal(expected, UpdateSettings.NormalizeRepository(input));
    }

    [Fact]
    public void SelectAsset_PicksPlatformSpecificPackage()
    {
        var release = new GitHubReleaseInfo
        {
            TagName = "v1.0.10",
            Version = "1.0.10",
            Assets = new[]
            {
                new GitHubReleaseAsset { Name = "UptimeKumaTrayAgent-Setup-1.0.10-x64.msi", BrowserDownloadUrl = "https://example.test/win-x64.msi" },
                new GitHubReleaseAsset { Name = "UptimeKumaTrayAgent-Setup-1.0.10-x86.msi", BrowserDownloadUrl = "https://example.test/win-x86.msi" },
                new GitHubReleaseAsset { Name = "uptime-kuma-agent-1.0.10-linux-x64.tar.gz", BrowserDownloadUrl = "https://example.test/linux-x64.tar.gz" },
                new GitHubReleaseAsset { Name = "uptime-kuma-agent-1.0.10-linux-arm64.tar.gz", BrowserDownloadUrl = "https://example.test/linux-arm64.tar.gz" }
            }
        };

        Assert.Equal("UptimeKumaTrayAgent-Setup-1.0.10-x64.msi", GitHubUpdateService.SelectAsset(release, AgentUpdatePlatform.WindowsX64)?.Name);
        Assert.Equal("UptimeKumaTrayAgent-Setup-1.0.10-x86.msi", GitHubUpdateService.SelectAsset(release, AgentUpdatePlatform.WindowsX86)?.Name);
        Assert.Equal("uptime-kuma-agent-1.0.10-linux-x64.tar.gz", GitHubUpdateService.SelectAsset(release, AgentUpdatePlatform.LinuxX64)?.Name);
        Assert.Equal("uptime-kuma-agent-1.0.10-linux-arm64.tar.gz", GitHubUpdateService.SelectAsset(release, AgentUpdatePlatform.LinuxArm64)?.Name);
    }
}
