using System.Windows;
using System.Windows.Media.Imaging;
using Thinksnap.App.Services;
using Thinksnap.Core.Annotations;

namespace Thinksnap.App;

public partial class EditorWindow : Window
{
    private readonly ExportService exportService = new();

    public EditorWindow(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        InitializeComponent();
        CanvasHost.SetImage(source);
    }

    private void PixelateButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasHost.ActiveTool = AnnotationTool.Pixelate;
    }

    private void ArrowButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasHost.ActiveTool = AnnotationTool.Arrow;
    }

    private void LineButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasHost.ActiveTool = AnnotationTool.Line;
    }

    private void RectangleButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasHost.ActiveTool = AnnotationTool.Rectangle;
    }

    private void PenButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasHost.ActiveTool = AnnotationTool.Pen;
    }

    private void TextButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasHost.ActiveTool = AnnotationTool.Text;
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasHost.Undo();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            exportService.CopyToClipboard(CanvasHost.RenderOutput());
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Copy failed: {ex.Message}", "Thinksnap", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            exportService.SavePng(CanvasHost.RenderOutput());
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Save failed: {ex.Message}", "Thinksnap", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
