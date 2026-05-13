using Thinksnap.Core.Imaging;
using Xunit;

namespace Thinksnap.Core.Tests;

public sealed class PixelateProcessorTests
{
    [Fact]
    public void PixelateFullRegionWithBlockSizeTwoAveragesAllPixels()
    {
        var pixels = new[]
        {
            new Rgba32(0, 10, 20, 30),
            new Rgba32(10, 20, 30, 40),
            new Rgba32(20, 30, 40, 50),
            new Rgba32(30, 40, 50, 60),
        };
        var expected = new Rgba32(15, 25, 35, 45);

        var result = PixelateProcessor.Pixelate(pixels, 2, 2, 0, 0, 2, 2, 2);

        Assert.Equal(new[] { expected, expected, expected, expected }, result);
    }

    [Fact]
    public void PixelateSinglePixelRegionLeavesOutsidePixelsUnchanged()
    {
        var pixels = new[]
        {
            new Rgba32(0, 0, 0, 255),
            new Rgba32(10, 10, 10, 255),
            new Rgba32(20, 20, 20, 255),
            new Rgba32(30, 30, 30, 255),
        };

        var result = PixelateProcessor.Pixelate(pixels, 2, 2, 1, 0, 1, 1, 1);

        Assert.Equal(pixels[0], result[0]);
        Assert.Equal(pixels[1], result[1]);
        Assert.Equal(pixels[2], result[2]);
        Assert.Equal(pixels[3], result[3]);
    }

    [Fact]
    public void PixelateRegionExceedingImageBoundsIsClamped()
    {
        var pixels = new[]
        {
            new Rgba32(0, 0, 0, 255),
            new Rgba32(20, 20, 20, 255),
            new Rgba32(40, 40, 40, 255),
            new Rgba32(60, 60, 60, 255),
        };
        var expected = new Rgba32(30, 30, 30, 255);

        var result = PixelateProcessor.Pixelate(pixels, 2, 2, -1, -1, 4, 4, 2);

        Assert.Equal(new[] { expected, expected, expected, expected }, result);
    }

    [Fact]
    public void PixelateDoesNotMutateInputArray()
    {
        var pixels = new[]
        {
            new Rgba32(0, 0, 0, 255),
            new Rgba32(100, 0, 0, 255),
            new Rgba32(0, 100, 0, 255),
            new Rgba32(0, 0, 100, 255),
        };
        var original = pixels.ToArray();

        _ = PixelateProcessor.Pixelate(pixels, 2, 2, 0, 0, 2, 2, 2);

        Assert.Equal(original, pixels);
    }
}
