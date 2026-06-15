namespace Thinksnap.App.Models;

public sealed record UpdateProgressState(
    string Message,
    double? Percentage = null,
    long DownloadedBytes = 0,
    long? TotalBytes = null);
