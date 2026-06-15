namespace Thinksnap.Core.Updates;

public static class UpdateCheckSchedule
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public static bool IsDue(DateTimeOffset? lastSuccessfulCheckUtc, DateTimeOffset nowUtc)
    {
        return lastSuccessfulCheckUtc is null || nowUtc - lastSuccessfulCheckUtc.Value >= Interval;
    }
}
