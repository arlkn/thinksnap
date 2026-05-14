using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Thinksnap.App.Services;
using Thinksnap.Core.Annotations;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfCursors = System.Windows.Input.Cursors;
using WindowsPoint = System.Windows.Point;

namespace Thinksnap.App;

public partial class SelectionOverlayWindow : Window
{
    private const int MinimumSelectionSize = 5;

    private readonly Bitmap screenCapture;
    private readonly CaptureService captureService;
    private readonly ExportService exportService = new();
    private readonly double scaleX;
    private readonly double scaleY;
    private readonly WpfButton[] toolButtons;

    private WindowsPoint? dragStart;

    public SelectionOverlayWindow(Bitmap screenCapture, CaptureService captureService)
    {
        ArgumentNullException.ThrowIfNull(screenCapture);
        ArgumentNullException.ThrowIfNull(captureService);

        InitializeComponent();

        this.screenCapture = screenCapture;
        this.captureService = captureService;
        toolButtons = [PixelateButton, ArrowButton, LineButton, RectangleButton, PenButton, TextButton];

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        scaleX = screenCapture.Width / Width;
        scaleY = screenCapture.Height / Height;

        SetActiveTool(AnnotationTool.Pixelate, PixelateButton);
    }

    public Int32Rect? SelectedRegion { get; private set; }

    public string ResultMessage { get; private set; } = "Capture canceled.";

    private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (EditorLayer.Visibility == Visibility.Visible)
        {
            return;
        }

        dragStart = e.GetPosition(OverlayCanvas);
        SelectedRegion = null;

        SelectionRectangle.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRectangle, dragStart.Value.X);
        Canvas.SetTop(SelectionRectangle, dragStart.Value.Y);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;

        OverlayCanvas.CaptureMouse();
    }

    private void OverlayCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (dragStart is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        UpdateSelectionRectangle(e.GetPosition(OverlayCanvas));
    }

    private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (dragStart is null)
        {
            return;
        }

        OverlayCanvas.ReleaseMouseCapture();
        var selectedRegion = NormalizeRegion(dragStart.Value, e.GetPosition(OverlayCanvas), scaleX, scaleY);
        dragStart = null;

        if (selectedRegion.Width < MinimumSelectionSize || selectedRegion.Height < MinimumSelectionSize)
        {
            SelectedRegion = null;
            ResultMessage = "Capture canceled.";
            DialogResult = false;
            return;
        }

        SelectedRegion = selectedRegion;
        EnterEditorMode(selectedRegion);
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        ResultMessage = EditorLayer.Visibility == Visibility.Visible ? "Edit canceled." : "Capture canceled.";
        DialogResult = false;
    }

    private void EnterEditorMode(Int32Rect selectedRegion)
    {
        using var croppedCapture = captureService.CropBitmap(screenCapture, selectedRegion);
        CanvasHost.SetImage(captureService.ToBitmapSource(croppedCapture));

        OverlayCanvas.Visibility = Visibility.Collapsed;
        SelectionRectangle.Visibility = Visibility.Collapsed;
        EditorLayer.Visibility = Visibility.Visible;
        Cursor = WpfCursors.Arrow;
        ResultMessage = $"Editing {selectedRegion.Width}x{selectedRegion.Height} region.";
    }

    private void PixelateButton_Click(object sender, RoutedEventArgs e) => SetActiveTool(AnnotationTool.Pixelate, PixelateButton);

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
            ResultMessage = "Capture copied to clipboard.";
            DialogResult = true;
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
            if (exportService.SavePng(CanvasHost.RenderOutput(), this))
            {
                ResultMessage = "Capture saved as PNG.";
                DialogResult = true;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Save failed: {ex.Message}", "Thinksnap", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ResultMessage = "Edit canceled.";
        DialogResult = false;
    }

    private void SetActiveTool(AnnotationTool tool, WpfButton activeButton)
    {
        CanvasHost.ActiveTool = tool;

        foreach (var button in toolButtons)
        {
            button.Background = WpfBrushes.Transparent;
            button.Foreground = WpfBrushes.White;
            button.BorderBrush = WpfBrushes.Transparent;
        }

        activeButton.Background = new SolidColorBrush(WpfColor.FromRgb(47, 128, 237));
        activeButton.Foreground = WpfBrushes.White;
    }

    private void UpdateSelectionRectangle(WindowsPoint current)
    {
        var region = NormalizeRegion(dragStart!.Value, current, scaleX: 1, scaleY: 1);

        Canvas.SetLeft(SelectionRectangle, region.X);
        Canvas.SetTop(SelectionRectangle, region.Y);
        SelectionRectangle.Width = region.Width;
        SelectionRectangle.Height = region.Height;
    }

    private static Int32Rect NormalizeRegion(WindowsPoint start, WindowsPoint end, double scaleX, double scaleY)
    {
        var left = (int)Math.Round(Math.Min(start.X, end.X) * scaleX);
        var top = (int)Math.Round(Math.Min(start.Y, end.Y) * scaleY);
        var right = (int)Math.Round(Math.Max(start.X, end.X) * scaleX);
        var bottom = (int)Math.Round(Math.Max(start.Y, end.Y) * scaleY);

        return new Int32Rect(left, top, right - left, bottom - top);
    }
}
