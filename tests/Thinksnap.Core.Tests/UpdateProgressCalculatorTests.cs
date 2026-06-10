using Thinksnap.Core.Updates;
using Xunit;

namespace Thinksnap.Core.Tests;

public sealed class UpdateProgressCalculatorTests
{
    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(25, 100, 25)]
    [InlineData(150, 100, 100)]
    public void CalculatePercentageReturnsClampedProgress(long downloadedBytes, long totalBytes, double expected)
    {
        Assert.Equal(expected, UpdateProgressCalculator.CalculatePercentage(downloadedBytes, totalBytes));
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    public void CalculatePercentageReturnsNullWhenTotalIsUnknown(long downloadedBytes, long totalBytes)
    {
        Assert.Null(UpdateProgressCalculator.CalculatePercentage(downloadedBytes, totalBytes));
    }
}
