namespace Thinksnap.Core.Updates;

public sealed record UpdateCandidate(string Tag, bool IsPrerelease);

public static class UpdateReleaseSelector
{
    public static UpdateCandidate? SelectLatest(IEnumerable<UpdateCandidate> releases, UpdateChannel channel)
    {
        ArgumentNullException.ThrowIfNull(releases);

        return releases
            .Where(release => channel == UpdateChannel.Beta || release.IsPrerelease is false)
            .Select(release => new { Release = release, Parsed = Parse(release.Tag) })
            .Where(item => item.Parsed is not null)
            .OrderByDescending(item => item.Parsed)
            .Select(item => item.Release)
            .FirstOrDefault();
    }

    private static SemanticVersion? Parse(string tag)
    {
        return SemanticVersion.TryParse(tag, out var version) ? version : null;
    }
}
