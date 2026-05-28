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

    public bool SavePng(BitmapSource bitmap, Window? owner = null, string? fileNamePattern = null)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".png",
            Filter = "PNG image (*.png)|*.png",
            FileName = CreateFileName(fileNamePattern),
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

    private static string CreateFileName(string? pattern)
    {
        var value = string.IsNullOrWhiteSpace(pattern)
            ? "thinksnap-{yyyyMMdd-HHmmss}.png"
            : pattern.Trim();

        var fileName = System.Text.RegularExpressions.Regex.Replace(
            value,
            "\\{([^{}]+)\\}",
            match =>
            {
                try
                {
                    return DateTime.Now.ToString(match.Groups[1].Value);
                }
                catch (FormatException)
                {
                    return string.Empty;
                }
            });

        if (fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) is false)
        {
            fileName += ".png";
        }

        foreach (var invalidCharacter in System.IO.Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidCharacter, '-');
        }

        return fileName;
    }
}
