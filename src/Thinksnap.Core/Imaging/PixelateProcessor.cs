namespace Thinksnap.Core.Imaging;

public readonly record struct Rgba32(byte R, byte G, byte B, byte A);

public static class PixelateProcessor
{
    public static Rgba32[] Pixelate(
        IReadOnlyList<Rgba32> pixels,
        int width,
        int height,
        int x,
        int y,
        int regionWidth,
        int regionHeight,
        int blockSize)
    {
        ArgumentNullException.ThrowIfNull(pixels);

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (blockSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize));
        }

        var expectedPixelCount = (long)width * height;
        if (pixels.Count != expectedPixelCount)
        {
            throw new ArgumentException("Pixel count must equal width multiplied by height.", nameof(pixels));
        }

        var result = pixels.ToArray();
        var left = Math.Clamp(x, 0, width);
        var top = Math.Clamp(y, 0, height);
        var right = (int)Math.Clamp((long)x + regionWidth, 0, width);
        var bottom = (int)Math.Clamp((long)y + regionHeight, 0, height);

        if (left >= right || top >= bottom)
        {
            return result;
        }

        for (var blockY = top; blockY < bottom;)
        {
            var blockBottom = blockY + Math.Min(blockSize, bottom - blockY);

            for (var blockX = left; blockX < right;)
            {
                var blockRight = blockX + Math.Min(blockSize, right - blockX);
                var average = Average(result, width, blockX, blockY, blockRight, blockBottom);

                Fill(result, width, blockX, blockY, blockRight, blockBottom, average);
                blockX = blockRight;
            }

            blockY = blockBottom;
        }

        return result;
    }

    private static Rgba32 Average(
        IReadOnlyList<Rgba32> pixels,
        int imageWidth,
        int left,
        int top,
        int right,
        int bottom)
    {
        var red = 0L;
        var green = 0L;
        var blue = 0L;
        var alpha = 0L;
        var count = 0L;

        for (var y = top; y < bottom; y++)
        {
            var rowOffset = y * imageWidth;

            for (var x = left; x < right; x++)
            {
                var pixel = pixels[rowOffset + x];
                red += pixel.R;
                green += pixel.G;
                blue += pixel.B;
                alpha += pixel.A;
                count++;
            }
        }

        return new Rgba32(
            (byte)(red / count),
            (byte)(green / count),
            (byte)(blue / count),
            (byte)(alpha / count));
    }

    private static void Fill(
        Rgba32[] pixels,
        int imageWidth,
        int left,
        int top,
        int right,
        int bottom,
        Rgba32 color)
    {
        for (var y = top; y < bottom; y++)
        {
            var rowOffset = y * imageWidth;

            for (var x = left; x < right; x++)
            {
                pixels[rowOffset + x] = color;
            }
        }
    }
}
