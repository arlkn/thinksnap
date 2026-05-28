namespace Thinksnap.Core.Updates;

public static class UpdateTarget
{
    public const string DefaultUrl = "https://github.com/arlkn/thinksnap/releases/latest";

    public static bool TryCreateUri(string? updateUrl, out Uri uri)
    {
        uri = new Uri(DefaultUrl);
        if (string.IsNullOrWhiteSpace(updateUrl))
        {
            return false;
        }

        return Uri.TryCreate(updateUrl.Trim(), UriKind.Absolute, out uri!) &&
            uri.Scheme is "https" or "http";
    }
}
