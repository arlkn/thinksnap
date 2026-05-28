using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using FormsSystemInformation = System.Windows.Forms.SystemInformation;
using FormsScreen = System.Windows.Forms.Screen;

namespace Thinksnap.App.Services;

public sealed class CaptureService
{
    public Bitmap CaptureVirtualScreen()
    {
        return CaptureBounds(FormsSystemInformation.VirtualScreen);
    }

    public Bitmap CaptureScreen(FormsScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);

        return CaptureBounds(screen.Bounds);
    }

    private static Bitmap CaptureBounds(DrawingRectangle bounds)
    {
        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(
            new DrawingPoint(bounds.Left, bounds.Top),
            DrawingPoint.Empty,
            bounds.Size,
            CopyPixelOperation.SourceCopy);

        return bitmap;
    }

    public Bitmap CropBitmap(Bitmap source, Int32Rect region)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "Crop region must have positive dimensions.");
        }

        if (region.X < 0 || region.Y < 0 || region.X + region.Width > source.Width || region.Y + region.Height > source.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "Crop region must fit inside the source bitmap.");
        }

        var rectangle = new DrawingRectangle(region.X, region.Y, region.Width, region.Height);
        return source.Clone(rectangle, PixelFormat.Format32bppArgb);
    }

    public BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;

        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var source = decoder.Frames[0];
        source.Freeze();
        return source;
    }
}
