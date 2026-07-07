using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Thinksnap.App.Models;
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
    private readonly AppSettings settings;
    private readonly ExportService exportService = new();
    private readonly double scaleX;
    private readonly double scaleY;
    private readonly WpfButton[] toolButtons;

    private Int32Rect activeEditorRegion;
    private bool editorFrameAdjusted;
    private bool editorFrameDragSurfaceEnabled;
    private WindowsPoint? dragStart;

    public SelectionOverlayWindow(Bitmap screenCapture, CaptureService captureService, Rect overlayBounds, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(screenCapture);
        ArgumentNullException.ThrowIfNull(captureService);
        ArgumentNullException.ThrowIfNull(settings);

        InitializeComponent();

        this.screenCapture = screenCapture;
        this.captureService = captureService;
        this.settings = settings;
        toolButtons = [PixelateButton, BlackoutButton, BlurButton, RepeatRedactionButton, ArrowButton, LineButton, RectangleButton, PenButton, TextButton];
        FrozenScreenImage.Source = captureService.ToBitmapSource(screenCapture);

        Left = overlayBounds.Left;
        Top = overlayBounds.Top;
        Width = Math.Max(1, overlayBounds.Width);
        Height = Math.Max(1, overlayBounds.Height);
        scaleX = screenCapture.Width / Width;
        scaleY = screenCapture.Height / Height;

        ApplyEditorDefaults();
        ApplyOverlayDefaults();
        ApplyLocalization();
        SetRedactionTool(CanvasHost.ActiveRedactionStyle, PixelateButton);
    }

    public Int32Rect? SelectedRegion { get; private set; }

    public string ResultMessage { get; private set; } = string.Empty;

    private void ApplyLocalization()
    {
        ResultMessage = T("Capture.Canceled");
        InstructionStepOneText.Text = T("Instruction.Step1");
        InstructionStepTwoText.Text = T("Instruction.Step2");
        InstructionStepThreeText.Text = T("Instruction.Step3");
        PixelateButton.ToolTip = T("Tool.Pixelate");
        BlackoutButton.ToolTip = T("Tool.Blackout");
        BlurButton.ToolTip = T("Tool.Blur");
        RepeatRedactionButton.ToolTip = T("Tool.RepeatRedaction");
        ArrowButton.ToolTip = T("Tool.Arrow");
        LineButton.ToolTip = T("Tool.Line");
        RectangleButton.ToolTip = T("Tool.Rectangle");
        PenButton.ToolTip = T("Tool.Pen");
        TextButton.ToolTip = T("Tool.Text");
        UndoButton.ToolTip = T("Action.Undo");
        DetachButton.ToolTip = T("Tool.Detach");
        CopyButton.ToolTip = T("Action.CopyToClipboard");
        SaveButton.ToolTip = T("Action.SavePng");
        CancelButton.ToolTip = T("Action.Cancel");
    }

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
        var selectionRect = NormalizeCanvasRect(dragStart.Value, e.GetPosition(OverlayCanvas));
        var selectedRegion = ToBitmapRegion(selectionRect);
        dragStart = null;

        if (selectedRegion.Width < MinimumSelectionSize || selectedRegion.Height < MinimumSelectionSize)
        {
            SelectedRegion = null;
            ResultMessage = T("Capture.Canceled");
            DialogResult = false;
            return;
        }

        SelectedRegion = selectedRegion;
        EnterEditorMode(selectedRegion);
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ResultMessage = EditorLayer.Visibility == Visibility.Visible ? T("Editor.Canceled") : T("Capture.Canceled");
            DialogResult = false;
        }
    }

    private void EnterEditorMode(Int32Rect selectedRegion)
    {
        LoadFullEditorImage();
        activeEditorRegion = selectedRegion;
        SelectedRegion = selectedRegion;
        editorFrameDragSurfaceEnabled = true;
        PositionEditorFrame(selectedRegion);

        OverlayCanvas.Visibility = Visibility.Collapsed;
        SelectionRectangle.Visibility = Visibility.Collapsed;
        InstructionPanel.Visibility = Visibility.Collapsed;
        EditorLayer.Visibility = Visibility.Visible;
        EditorLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
        Cursor = WpfCursors.Arrow;
        ResultMessage = L("Editor.EditingRegion", selectedRegion.Width, selectedRegion.Height);
    }

    private void EditorFrameAdjust_DragStarted(object sender, DragStartedEventArgs e)
    {
        editorFrameAdjusted = false;
    }

    private void EditorFrameDragSurface_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var movedFrame = SelectionFrameGeometry.MoveWithinBounds(
            new Rect(GetCanvasLeft(EditorFrame), GetCanvasTop(EditorFrame), EditorFrame.Width, EditorFrame.Height),
            new Vector(e.HorizontalChange, e.VerticalChange),
            new System.Windows.Size(Width, Height));

        SetEditorFrameVisualRect(movedFrame);
        editorFrameAdjusted = true;
        UpdateActiveRegionFromFrame();
        PositionEditorChrome();
    }

    private void EditorFrameResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var handle = (sender as FrameworkElement)?.Tag?.ToString() ?? "BottomRight";
        var frameLeft = GetCanvasLeft(EditorFrame);
        var frameTop = GetCanvasTop(EditorFrame);
        var frameRight = frameLeft + EditorFrame.Width;
        var frameBottom = frameTop + EditorFrame.Height;
        var minWidth = Math.Max(24, MinimumSelectionSize / scaleX);
        var minHeight = Math.Max(24, MinimumSelectionSize / scaleY);

        if (handle.Contains("Left", StringComparison.OrdinalIgnoreCase))
        {
            frameLeft = Math.Clamp(frameLeft + e.HorizontalChange, 0, frameRight - minWidth);
        }

        if (handle.Contains("Right", StringComparison.OrdinalIgnoreCase))
        {
            frameRight = Math.Clamp(frameRight + e.HorizontalChange, frameLeft + minWidth, Width);
        }

        if (handle.Contains("Top", StringComparison.OrdinalIgnoreCase))
        {
            frameTop = Math.Clamp(frameTop + e.VerticalChange, 0, frameBottom - minHeight);
        }

        if (handle.Contains("Bottom", StringComparison.OrdinalIgnoreCase))
        {
            frameBottom = Math.Clamp(frameBottom + e.VerticalChange, frameTop + minHeight, Height);
        }

        SetEditorFrameVisualRect(new Rect(frameLeft, frameTop, frameRight - frameLeft, frameBottom - frameTop));
        editorFrameAdjusted = true;
        UpdateActiveRegionFromFrame();
        PositionEditorChrome();
    }

    private void EditorFrameAdjust_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (editorFrameAdjusted is false)
        {
            return;
        }

        CommitEditorFrameRegion();
        editorFrameAdjusted = false;
    }

    private void PositionEditorFrame(Int32Rect selectedRegion)
    {
        EditorViewport.Width = Width;
        EditorViewport.Height = Height;
        Canvas.SetLeft(EditorViewport, 0);
        Canvas.SetTop(EditorViewport, 0);

        var visualLeft = selectedRegion.X / scaleX;
        var visualTop = selectedRegion.Y / scaleY;
        var visualWidth = selectedRegion.Width / scaleX;
        var visualHeight = selectedRegion.Height / scaleY;

        SetEditorFrameVisualRect(new Rect(visualLeft, visualTop, visualWidth, visualHeight));

        PositionEditorChrome();
    }

    private void PositionEditorChrome()
    {
        PositionFrameDragSurface();
        PositionResizeHandles();
        PositionToolbarNearFrame();
    }

    private void PositionFrameDragSurface()
    {
        Canvas.SetLeft(EditorFrameDragSurface, GetCanvasLeft(EditorFrame));
        Canvas.SetTop(EditorFrameDragSurface, GetCanvasTop(EditorFrame));
        EditorFrameDragSurface.Width = EditorFrame.Width;
        EditorFrameDragSurface.Height = EditorFrame.Height;
        EditorFrameDragSurface.Visibility = editorFrameDragSurfaceEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PositionResizeHandles()
    {
        var left = GetCanvasLeft(EditorFrame);
        var top = GetCanvasTop(EditorFrame);
        var right = left + EditorFrame.Width;
        var bottom = top + EditorFrame.Height;
        var centerX = left + (EditorFrame.Width / 2);
        var centerY = top + (EditorFrame.Height / 2);

        PositionThumb(ResizeTopLeftThumb, left, top);
        PositionThumb(ResizeTopThumb, centerX, top);
        PositionThumb(ResizeTopRightThumb, right, top);
        PositionThumb(ResizeRightThumb, right, centerY);
        PositionThumb(EditorFrameResizeThumb, right, bottom);
        PositionThumb(ResizeBottomThumb, centerX, bottom);
        PositionThumb(ResizeBottomLeftThumb, left, bottom);
        PositionThumb(ResizeLeftThumb, left, centerY);
    }

    private static void PositionThumb(Thumb thumb, double centerX, double centerY)
    {
        Canvas.SetLeft(thumb, centerX - (thumb.Width / 2));
        Canvas.SetTop(thumb, centerY - (thumb.Height / 2));
    }

    private void PositionToolbarNearFrame()
    {
        EditorToolbar.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var visualLeft = GetCanvasLeft(EditorFrame);
        var visualTop = GetCanvasTop(EditorFrame);
        var toolbarWidth = EditorToolbar.DesiredSize.Width;
        var toolbarHeight = EditorToolbar.DesiredSize.Height;
        var toolbarLeft = visualLeft + ((EditorFrame.Width - toolbarWidth) / 2);
        toolbarLeft = Math.Clamp(toolbarLeft, 12, Math.Max(12, Width - toolbarWidth - 12));

        var toolbarTop = visualTop + EditorFrame.Height + 12;
        if (toolbarTop + toolbarHeight > Height - 12)
        {
            toolbarTop = visualTop - toolbarHeight - 12;
        }

        toolbarTop = Math.Clamp(toolbarTop, 12, Math.Max(12, Height - toolbarHeight - 12));
        Canvas.SetLeft(EditorToolbar, toolbarLeft);
        Canvas.SetTop(EditorToolbar, toolbarTop);
    }

    private void CommitEditorFrameRegion()
    {
        if (UpdateActiveRegionFromFrame() is false)
        {
            PositionEditorFrame(activeEditorRegion);
            return;
        }

        PositionEditorChrome();
        ResultMessage = L("Editor.EditingRegion", activeEditorRegion.Width, activeEditorRegion.Height);
    }

    private void LoadFullEditorImage()
    {
        CanvasHost.ResetImage(captureService.ToBitmapSource(screenCapture));
        ApplyEditorDefaults();
    }

    private void ApplyEditorDefaults()
    {
        CanvasHost.StrokeColor = settings.DefaultTextColor;
        CanvasHost.NewTextPlaceholder = T("Tool.Text");
        CanvasHost.TextFontSize = Math.Clamp(settings.DefaultTextFontSize, 10, 72);
        CanvasHost.StrokeThickness = Math.Clamp(settings.DefaultStrokeThickness, 1, 12);
        CanvasHost.IsTextBold = settings.DefaultTextBold;
        CanvasHost.TextAlignment = Enum.TryParse<TextAnnotationAlignment>(
            settings.DefaultTextAlignment,
            ignoreCase: true,
            out var textAlignment)
            ? textAlignment
            : TextAnnotationAlignment.Left;
        CanvasHost.TextBackgroundColor = settings.DefaultTextBackgroundEnabled
            ? settings.DefaultTextBackgroundColor
            : null;
        CanvasHost.ActiveRedactionStyle = Enum.TryParse<RedactionStyle>(
            settings.DefaultRedactionStyle,
            ignoreCase: true,
            out var redactionStyle)
            ? redactionStyle
            : RedactionStyle.Pixelate;
    }

    private void ApplyOverlayDefaults()
    {
        InstructionPanel.Visibility = settings.ShowSelectionInstructions ? Visibility.Visible : Visibility.Collapsed;

        var dimOpacity = Math.Clamp(settings.OverlayDimOpacity, 0.2, 0.85);
        OverlayCanvas.Background = new SolidColorBrush(WpfColor.FromArgb((byte)(dimOpacity * 255), 0, 0, 0));
        EditorLayer.Background = new SolidColorBrush(WpfColor.FromArgb((byte)(Math.Min(0.9, dimOpacity + 0.15) * 255), 17, 20, 24));
        ApplyToolbarButtonSize();
    }

    private void ApplyToolbarButtonSize()
    {
        var size = Math.Clamp(settings.OverlayToolbarButtonSize, 36, 56);
        foreach (var button in FindVisualChildren<WpfButton>(EditorToolbar))
        {
            button.Width = size;
            button.Height = size;
            button.Padding = new Thickness(Math.Clamp(size * 0.22, 6, 12));
        }

        EditorToolbar.CornerRadius = new CornerRadius((size + 12) / 2);
    }

    private bool UpdateActiveRegionFromFrame()
    {
        var selectedRegion = ToBitmapRegion(new Rect(
            GetCanvasLeft(EditorFrame),
            GetCanvasTop(EditorFrame),
            EditorFrame.Width,
            EditorFrame.Height));

        if (selectedRegion.Width < MinimumSelectionSize || selectedRegion.Height < MinimumSelectionSize)
        {
            return false;
        }

        activeEditorRegion = selectedRegion;
        SelectedRegion = selectedRegion;
        return true;
    }

    private void PixelateButton_Click(object sender, RoutedEventArgs e) => SetRedactionTool(RedactionStyle.Pixelate, PixelateButton);

    private void BlackoutButton_Click(object sender, RoutedEventArgs e) => SetRedactionTool(RedactionStyle.Blackout, BlackoutButton);

    private void BlurButton_Click(object sender, RoutedEventArgs e) => SetRedactionTool(RedactionStyle.Blur, BlurButton);

    private void RepeatRedactionButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasHost.RepeatLastRedactionStyle();
        SetActiveTool(AnnotationTool.Pixelate, RepeatRedactionButton);
    }

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
            exportService.CopyToClipboard(RenderSelectedOutput());
            ResultMessage = T("Capture.Copied");
            DialogResult = true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, L("Error.CopyFailed", ex.Message), "Thinksnap", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var output = RenderSelectedOutput();
            if (exportService.SavePng(output, this, settings.DefaultFileNamePattern))
            {
                if (settings.CopyAfterSave)
                {
                    exportService.CopyToClipboard(output);
                }

                ResultMessage = settings.CopyAfterSave
                    ? T("Capture.SavedAndCopied")
                    : T("Capture.Saved");
                DialogResult = true;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, L("Error.SaveFailed", ex.Message), "Thinksnap", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ResultMessage = T("Editor.Canceled");
        DialogResult = false;
    }

    private void SetActiveTool(AnnotationTool tool, WpfButton activeButton)
    {
        editorFrameDragSurfaceEnabled = false;
        PositionFrameDragSurface();
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
        activeButton.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        activeButton.RenderTransform = new ScaleTransform(1, 1);
        activeButton.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(120)));
        activeButton.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(120)));
    }

    private void DetachButton_Click(object sender, RoutedEventArgs e)
    {
        var detachedWindow = new DetachedEditorWindow(RenderSelectedOutput(), exportService, settings)
        {
            Owner = Owner
        };
        detachedWindow.Show();
        ResultMessage = L("Editor.EditingRegionDetached", activeEditorRegion.Width, activeEditorRegion.Height);
        DialogResult = true;
    }

    private void SetRedactionTool(RedactionStyle style, WpfButton activeButton)
    {
        CanvasHost.ActiveRedactionStyle = style;
        SetActiveTool(AnnotationTool.Pixelate, activeButton);
    }

    private void UpdateSelectionRectangle(WindowsPoint current)
    {
        ApplySelectionRect(NormalizeCanvasRect(dragStart!.Value, current));
    }

    private void ApplySelectionRect(Rect region)
    {
        Canvas.SetLeft(SelectionRectangle, region.X);
        Canvas.SetTop(SelectionRectangle, region.Y);
        SelectionRectangle.Width = region.Width;
        SelectionRectangle.Height = region.Height;
    }

    private Int32Rect ToBitmapRegion(Rect selection)
    {
        var left = Math.Clamp((int)Math.Round(selection.X * scaleX), 0, screenCapture.Width);
        var top = Math.Clamp((int)Math.Round(selection.Y * scaleY), 0, screenCapture.Height);
        var right = Math.Clamp((int)Math.Round(selection.Right * scaleX), 0, screenCapture.Width);
        var bottom = Math.Clamp((int)Math.Round(selection.Bottom * scaleY), 0, screenCapture.Height);

        return new Int32Rect(left, top, right - left, bottom - top);
    }

    private static Rect NormalizeCanvasRect(WindowsPoint start, WindowsPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var right = Math.Max(start.X, end.X);
        var bottom = Math.Max(start.Y, end.Y);

        return new Rect(left, top, right - left, bottom - top);
    }

    private void SetEditorFrameVisualRect(Rect region)
    {
        Canvas.SetLeft(EditorFrame, region.X);
        Canvas.SetTop(EditorFrame, region.Y);
        EditorFrame.Width = region.Width;
        EditorFrame.Height = region.Height;
    }

    private BitmapSource RenderSelectedOutput()
    {
        UpdateActiveRegionFromFrame();
        var output = CanvasHost.RenderOutput();
        var cropX = Math.Clamp(activeEditorRegion.X, 0, output.PixelWidth - 1);
        var cropY = Math.Clamp(activeEditorRegion.Y, 0, output.PixelHeight - 1);
        var cropWidth = Math.Clamp(activeEditorRegion.Width, 1, output.PixelWidth - cropX);
        var cropHeight = Math.Clamp(activeEditorRegion.Height, 1, output.PixelHeight - cropY);
        var cropRegion = new Int32Rect(
            cropX,
            cropY,
            cropWidth,
            cropHeight);
        var cropped = new CroppedBitmap(output, cropRegion);
        cropped.Freeze();
        return cropped;
    }

    private static double GetCanvasLeft(UIElement element)
    {
        var left = Canvas.GetLeft(element);
        return double.IsNaN(left) ? 0 : left;
    }

    private static double GetCanvasTop(UIElement element)
    {
        var top = Canvas.GetTop(element);
        return double.IsNaN(top) ? 0 : top;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private string T(string key) => LocalizationService.Text(settings, key);

    private string L(string key, params object[] values) => LocalizationService.Format(settings, key, values);
}
