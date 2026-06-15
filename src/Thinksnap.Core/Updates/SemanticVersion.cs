namespace Thinksnap.Core.Updates;

public sealed record SemanticVersion(int Major, int Minor, int Patch, IReadOnlyList<string> Prerelease)
    : IComparable<SemanticVersion>
{
    public bool IsPrerelease => Prerelease.Count > 0;

    public static SemanticVersion Parse(string value)
    {
        if (TryParse(value, out var version) is false)
        {
            throw new FormatException($"Invalid semantic version: {value}");
        }

        return version;
    }

    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = new SemanticVersion(0, 0, 0, Array.Empty<string>());
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().TrimStart('v', 'V');
        var metadataIndex = normalized.IndexOf('+');
        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }

        var parts = normalized.Split('-', 2);
        var numbers = parts[0].Split('.');
        if (numbers.Length is < 2 or > 3 ||
            int.TryParse(numbers[0], out var major) is false ||
            int.TryParse(numbers[1], out var minor) is false ||
            (numbers.Length > 2 && int.TryParse(numbers[2], out _) is false))
        {
            return false;
        }

        var patch = numbers.Length > 2 ? int.Parse(numbers[2]) : 0;
        var prerelease = parts.Length == 2
            ? parts[1].Split('.', StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>();
        version = new SemanticVersion(major, minor, patch, prerelease);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var core = Major.CompareTo(other.Major);
        if (core == 0) core = Minor.CompareTo(other.Minor);
        if (core == 0) core = Patch.CompareTo(other.Patch);
        if (core != 0) return core;
        if (IsPrerelease != other.IsPrerelease) return IsPrerelease ? -1 : 1;

        for (var i = 0; i < Math.Max(Prerelease.Count, other.Prerelease.Count); i++)
        {
            if (i >= Prerelease.Count) return -1;
            if (i >= other.Prerelease.Count) return 1;
            var leftNumeric = int.TryParse(Prerelease[i], out var leftNumber);
            var rightNumeric = int.TryParse(other.Prerelease[i], out var rightNumber);
            var comparison = leftNumeric && rightNumeric
                ? leftNumber.CompareTo(rightNumber)
                : leftNumeric ? -1
                : rightNumeric ? 1
                : string.Compare(Prerelease[i], other.Prerelease[i], StringComparison.OrdinalIgnoreCase);
            if (comparison != 0) return comparison;
        }

        return 0;
    }
}
