using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Thinksnap.App.Services;
using Thinksnap.Core.Annotations;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;

namespace Thinksnap.App;

public partial class DetachedEditorWindow : Window
{
    private readonly ExportService exportService;
    private readonly WpfButton[] toolButtons;

    public DetachedEditorWindow(BitmapSource source, ExportService exportService)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(exportService);

        this.exportService = exportService;
        InitializeComponent();
        toolButtons = [PixelateButton, BlackoutButton, BlurButton, ArrowButton, LineButton, RectangleButton, PenButton, TextButton];
        CanvasHost.SetImage(source);
        SetActiveTool(AnnotationTool.Pixelate, PixelateButton);
    }

    private void PixelateButton_Click(object sender, RoutedEventArgs e) => SetRedactionTool(RedactionStyle.Pixelate, PixelateButton);

    private void BlackoutButton_Click(object sender, RoutedEventArgs e) => SetRedactionTool(RedactionStyle.Blackout, BlackoutButton);

    private void BlurButton_Click(object sender, RoutedEventArgs e) => SetRedactionTool(RedactionStyle.Blur, BlurButton);

    private void ArrowButton_Click(object sender, RoutedEventArgs e) => SetActiveTool(AnnotationTool.Arrow, ArrowButton);

    private void LineButton_Click(object sender, RoutedEventArgs e) => SetActiveTool(AnnotationTool.Line, LineButton);

    private void RectangleButton_Click(object sender, RoutedEventArgs e) => SetActiveTool(AnnotationTool.Rectangle, RectangleButton);

    private void PenButton_Click(object sender, RoutedEventArgs e) => SetActiveTool(AnnotationTool.Pen, PenButton);

    private void TextButton_Click(object sender, RoutedEventArgs e) => SetActiveTool(AnnotationTool.Text, TextButton);

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
            _ = exportService.SavePng(CanvasHost.RenderOutput(), this);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Save failed: {ex.Message}", "Thinksnap", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetActiveTool(AnnotationTool tool, WpfButton activeButton)
    {
        CanvasHost.ActiveTool = tool;

        foreach (var button in toolButtons)
        {
            button.Background = WpfBrushes.Transparent;
            button.Foreground = WpfBrushes.White;
            button.BorderBrush = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["BorderBrushColor"];
        }

        activeButton.Background = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["AccentBrush"];
        activeButton.Foreground = WpfBrushes.White;
        activeButton.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(156, 200, 255));
    }

    private void SetRedactionTool(RedactionStyle style, WpfButton activeButton)
    {
        CanvasHost.ActiveRedactionStyle = style;
        SetActiveTool(AnnotationTool.Pixelate, activeButton);
    }
}
