using Thinksnap.Core.Updates;
using Xunit;

namespace Thinksnap.Core.Tests;

public sealed class SecureUpdatePolicyTests
{
    [Fact]
    public void StableChannelRejectsPrereleases()
    {
        var releases = new[]
        {
            new UpdateCandidate("v0.1.2-beta.2", true),
            new UpdateCandidate("v0.1.1", false)
        };

        var selected = UpdateReleaseSelector.SelectLatest(releases, UpdateChannel.Stable);

        Assert.Equal("v0.1.1", selected?.Tag);
    }

    [Fact]
    public void BetaChannelSelectsNewestStableOrPrerelease()
    {
        var releases = new[]
        {
            new UpdateCandidate("v0.1.2-beta.1", true),
            new UpdateCandidate("v0.1.2-beta.3", true),
            new UpdateCandidate("v0.1.1", false)
        };

        var selected = UpdateReleaseSelector.SelectLatest(releases, UpdateChannel.Beta);

        Assert.Equal("v0.1.2-beta.3", selected?.Tag);
    }

    [Theory]
    [InlineData("v0.1.2", "v0.1.2-beta.9", 1)]
    [InlineData("v0.1.2-beta.10", "v0.1.2-beta.2", 1)]
    [InlineData("v0.1.2-beta.1", "v0.1.2-beta.1", 0)]
    public void SemanticVersionsComparePrereleaseIdentifiers(string left, string right, int expectedSign)
    {
        var comparison = SemanticVersion.Parse(left).CompareTo(SemanticVersion.Parse(right));

        Assert.Equal(expectedSign, Math.Sign(comparison));
    }

    [Theory]
    [InlineData("v0.1.2", "0.1.2")]
    [InlineData("0.1.2-beta.3", "0.1.2-beta.3")]
    public void SemanticVersionsUseUserFriendlyText(string value, string expected)
    {
        Assert.Equal(expected, SemanticVersion.Parse(value).ToString());
    }

    [Fact]
    public void AutomaticCheckIsDueAfterTwentyFourHours()
    {
        var now = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        Assert.False(UpdateCheckSchedule.IsDue(now.AddHours(-23), now));
        Assert.True(UpdateCheckSchedule.IsDue(now.AddHours(-24), now));
        Assert.True(UpdateCheckSchedule.IsDue(null, now));
    }

    [Fact]
    public void VerificationRequiresAllThreeMatchingDigests()
    {
        const string digest = "915024afdf4d9c4a32ef9e09bf174434fd2db03fb14a25297eb4fe300b5b562c";

        Assert.True(UpdateHashPolicy.Verify($"sha256:{digest}", digest.ToUpperInvariant(), digest).IsValid);
        Assert.False(UpdateHashPolicy.Verify(null, digest, digest).IsValid);
        Assert.False(UpdateHashPolicy.Verify($"sha256:{digest}", new string('0', 64), digest).IsValid);
    }
}
