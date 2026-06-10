namespace Thinksnap.Core.Updates;

public static class UpdateProgressCalculator
{
    public static double? CalculatePercentage(long downloadedBytes, long? totalBytes)
    {
        if (totalBytes is null or <= 0)
        {
            return null;
        }

        var percentage = downloadedBytes * 100d / totalBytes.Value;
        return Math.Clamp(percentage, 0, 100);
    }
}
