using Thinksnap.Core.Updates;
using Xunit;

namespace Thinksnap.Core.Tests;

public sealed class UpdateTargetTests
{
    [Fact]
    public void DefaultUrlTargetsLatestGitHubRelease()
    {
        Assert.Equal("https://github.com/arlkn/thinksnap/releases/latest", UpdateTarget.DefaultUrl);
    }

    [Theory]
    [InlineData("https://github.com/arlkn/thinksnap/releases/latest")]
    [InlineData(" http://localhost/releases/latest ")]
    public void TryCreateUriAcceptsHttpUrls(string updateUrl)
    {
        var accepted = UpdateTarget.TryCreateUri(updateUrl, out var uri);

        Assert.True(accepted);
        Assert.True(uri.Scheme is "https" or "http");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("file:///C:/temp/update.exe")]
    public void TryCreateUriRejectsMissingOrUnsafeUrls(string updateUrl)
    {
        var accepted = UpdateTarget.TryCreateUri(updateUrl, out _);

        Assert.False(accepted);
    }
}
