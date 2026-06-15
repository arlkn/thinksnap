namespace Thinksnap.Core.Updates;

public static class UpdateInstallCompletionPolicy
{
    public static bool IsCompleted(string? pendingVersion, string? currentVersion)
    {
        return SemanticVersion.TryParse(pendingVersion, out var pending) &&
            SemanticVersion.TryParse(currentVersion, out var current) &&
            current.CompareTo(pending) >= 0;
    }
}
