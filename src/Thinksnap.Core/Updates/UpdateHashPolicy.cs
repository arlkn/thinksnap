namespace Thinksnap.Core.Updates;

public sealed record UpdateHashVerification(bool IsValid, string? NormalizedDigest, string? Error);

public static class UpdateHashPolicy
{
    public static UpdateHashVerification Verify(string? githubDigest, string? checksumDigest, string? calculatedDigest)
    {
        var github = Normalize(githubDigest);
        var checksum = Normalize(checksumDigest);
        var calculated = Normalize(calculatedDigest);
        if (github is null || checksum is null || calculated is null)
        {
            return new UpdateHashVerification(false, null, "All SHA-256 verification sources are required.");
        }

        return github == checksum && checksum == calculated
            ? new UpdateHashVerification(true, calculated, null)
            : new UpdateHashVerification(false, calculated, "SHA-256 verification values do not match.");
    }

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[7..];
        }

        normalized = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return normalized.Length == 64 && normalized.All(Uri.IsHexDigit)
            ? normalized.ToLowerInvariant()
            : null;
    }
}
