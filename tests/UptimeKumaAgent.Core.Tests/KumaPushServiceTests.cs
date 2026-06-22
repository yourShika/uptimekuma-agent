using UptimeKumaTrayAgent.Models;
using UptimeKumaTrayAgent.Services;
using UptimeKumaTrayAgent.Utils;
using Xunit;

namespace UptimeKumaAgent.Core.Tests;

public sealed class KumaPushServiceTests
{
    [Fact]
    public void BuildPushUri_MergesStatusMessageAndPing()
    {
        var checkResult = MonitorCheckResult.Up("Ping OK", 42);

        var ok = KumaPushService.BuildPushUri(
            "https://kuma.example.com/api/push/secret-token?existing=1&status=down",
            checkResult,
            out var uri,
            out var error);

        Assert.True(ok, error);
        Assert.NotNull(uri);
        Assert.Contains("existing=1", uri!.Query);
        Assert.Contains("status=up", uri.Query);
        Assert.Contains("msg=Ping%20OK", uri.Query);
        Assert.Contains("ping=42", uri.Query);
        Assert.DoesNotContain("status=down", uri.Query);
    }

    [Fact]
    public void UrlMasker_MasksPushSecrets()
    {
        var masked = UrlMasker.MaskPotentialUrls("push https://kuma.example.com/api/push/secret-token?x=1");

        Assert.Contains("https://kuma.example.com/api/push/****", masked);
        Assert.DoesNotContain("secret-token", masked);
    }
}
