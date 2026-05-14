using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Thinksnap.App.Services;

public sealed class ExportService
{
    public void CopyToClipboard(BitmapSource bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        System.Windows.Clipboard.SetImage(bitmap);
    }

    public bool SavePng(BitmapSource bitmap, Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".png",
            Filter = "PNG image (*.png)|*.png",
            FileName = "thinksnap.png",
            OverwritePrompt = true
        };

        var accepted = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        if (accepted != true)
        {
            return false;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = dialog.OpenFile();
        encoder.Save(stream);

        return true;
    }
}
