using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Thinksnap.Core.Annotations;
using Thinksnap.Core.Imaging;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfImage = System.Windows.Controls.Image;
using WpfPoint = System.Windows.Point;
using WpfGrid = System.Windows.Controls.Grid;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace Thinksnap.App.Controls;

public sealed class AnnotationCanvas : Canvas
{
    private const string DefaultStroke = "#ff0000";
    private const double DefaultStrokeThickness = 3;
    private const double MinimumDragDistance = 2;
    private const int PixelateBlockSize = 12;
    private const double DefaultTextFontSize = 18;

    private readonly WpfImage baseImage = new();
    private readonly List<AnnotationOperation> operations = [];
    private readonly List<UndoEntry> undoEntries = [];
    private readonly List<PointD> penPoints = [];

    private WriteableBitmap? baseBitmap;
    private WpfPoint? dragStart;
    private UIElement[]? activeVisuals;
    private WpfGrid? selectedTextContainer;
    private WpfTextBox? selectedTextBox;
    private Border? textToolbar;
    private RedactionStyle lastRedactionStyle = RedactionStyle.Pixelate;

    public AnnotationCanvas()
    {
        Background = WpfBrushes.Transparent;
        ClipToBounds = true;
        Children.Add(baseImage);
    }

    public AnnotationTool ActiveTool { get; set; } = AnnotationTool.Pixelate;

    public RedactionStyle ActiveRedactionStyle { get; set; } = RedactionStyle.Pixelate;

    public string StrokeColor { get; set; } = DefaultStroke;

    public double StrokeThickness { get; set; } = DefaultStrokeThickness;

    public double TextFontSize { get; set; } = DefaultTextFontSize;

    public bool IsTextBold { get; set; }

    public TextAnnotationAlignment TextAlignment { get; set; } = TextAnnotationAlignment.Left;

    public string? TextBackgroundColor { get; set; }

    public bool ShowTextMoveHandles { get; set; } = true;

    public string NewTextPlaceholder { get; set; } = "Text";

    public IReadOnlyList<AnnotationOperation> Operations => operations.ToArray();

    public void SetImage(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        baseBitmap = new WriteableBitmap(converted);
        baseImage.Source = baseBitmap;
        baseImage.Width = baseBitmap.PixelWidth;
        baseImage.Height = baseBitmap.PixelHeight;
        Width = baseBitmap.PixelWidth;
        Height = baseBitmap.PixelHeight;
    }

    public void ResetImage(BitmapSource source)
    {
        Children.Clear();
        Children.Add(baseImage);
        operations.Clear();
        undoEntries.Clear();
        penPoints.Clear();
        activeVisuals = null;
        dragStart = null;
        selectedTextContainer = null;
        selectedTextBox = null;
        textToolbar = null;

        SetImage(source);
    }

    public void Undo()
    {
        if (undoEntries.Count == 0)
        {
            return;
        }

        var entry = undoEntries[^1];
        undoEntries.RemoveAt(undoEntries.Count - 1);

        foreach (var visual in entry.Visuals)
        {
            Children.Remove(visual);
        }

        if (entry.PreviousBitmapPixels is not null)
        {
            RestoreBitmapPixels(entry.PreviousBitmapPixels);
        }

        if (operations.Count > 0)
        {
            operations.RemoveAt(operations.Count - 1);
        }
    }

    public RenderTargetBitmap RenderOutput()
    {
        SetEditingChromeVisibility(Visibility.Collapsed);
        UpdateLayout();

        try
        {
            var width = Math.Max(1, (int)Math.Ceiling(Width));
            var height = Math.Max(1, (int)Math.Ceiling(Height));
            var output = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            output.Render(this);
            output.Freeze();

            return output;
        }
        finally
        {
            SetEditingChromeVisibility(Visibility.Visible);
        }
    }

    public void RepeatLastRedactionStyle()
    {
        ActiveTool = AnnotationTool.Pixelate;
        ActiveRedactionStyle = lastRedactionStyle;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        var position = e.GetPosition(this);
        dragStart = position;

        if (ActiveTool == AnnotationTool.Text)
        {
            AddText(position);
            dragStart = null;
            e.Handled = true;
            return;
        }

        activeVisuals = CreateActiveVisuals(position);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (dragStart is null || activeVisuals is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        UpdateActiveVisuals(dragStart.Value, position);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (dragStart is null || activeVisuals is null)
        {
            return;
        }

        var start = dragStart.Value;
        var end = e.GetPosition(this);

        ReleaseMouseCapture();
        UpdateActiveVisuals(start, end);

        if (IsMeaningfulOperation(start, end))
        {
            AddOperation(start, end, activeVisuals);
        }
        else
        {
            RemoveVisuals(activeVisuals);
        }

        dragStart = null;
        activeVisuals = null;
        penPoints.Clear();
        e.Handled = true;
    }

    private UIElement[] CreateActiveVisuals(WpfPoint start)
    {
        return ActiveTool switch
        {
            AnnotationTool.Line => AddLine(start),
            AnnotationTool.Arrow => AddArrow(start),
            AnnotationTool.Rectangle => AddRectangle(start, fill: WpfBrushes.Transparent),
            AnnotationTool.Pixelate => AddPixelateSelection(start),
            AnnotationTool.Pen => AddPen(start),
            _ => []
        };
    }

    private UIElement[] AddLine(WpfPoint start)
    {
        var line = new Line
        {
            X1 = start.X,
            Y1 = start.Y,
            X2 = start.X,
            Y2 = start.Y,
            Stroke = CreateStrokeBrush(),
            StrokeThickness = StrokeThickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        Children.Add(line);
        return [line];
    }

    private UIElement[] AddArrow(WpfPoint start)
    {
        var line = (Line)AddLine(start)[0];
        var head = new Polygon
        {
            Fill = CreateStrokeBrush(),
            Stroke = CreateStrokeBrush(),
            StrokeThickness = 1
        };

        Children.Add(head);
        return [line, head];
    }

    private UIElement[] AddRectangle(WpfPoint start, WpfBrush fill)
    {
        var rectangle = new WpfRectangle
        {
            Fill = fill,
            Stroke = CreateStrokeBrush(),
            StrokeThickness = StrokeThickness
        };

        SetLeft(rectangle, start.X);
        SetTop(rectangle, start.Y);
        Children.Add(rectangle);
        return [rectangle];
    }

    private UIElement[] AddPen(WpfPoint start)
    {
        penPoints.Clear();
        penPoints.Add(ToPointD(start));

        var polyline = new Polyline
        {
            Stroke = CreateStrokeBrush(),
            StrokeThickness = StrokeThickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Points = new PointCollection { start }
        };

        Children.Add(polyline);
        return [polyline];
    }

    private UIElement[] AddPixelateSelection(WpfPoint start)
    {
        var rectangle = new WpfRectangle
        {
            Fill = WpfBrushes.Transparent,
            Stroke = WpfBrushes.White,
            StrokeThickness = 1,
            StrokeDashArray = [4, 3],
            IsHitTestVisible = false
        };

        SetLeft(rectangle, start.X);
        SetTop(rectangle, start.Y);
        Children.Add(rectangle);
        return [rectangle];
    }

    private void AddText(WpfPoint start)
    {
        var operationIndex = operations.Count;
        var container = new WpfGrid
        {
            Width = 160,
            Height = 48,
            MinWidth = 80,
            MinHeight = 32
        };

        var textBox = new WpfTextBox
        {
            Text = NewTextPlaceholder,
            Foreground = CreateStrokeBrush(),
            Background = CreateTextBackgroundBrush(),
            BorderBrush = CreateStrokeBrush(),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 18, 4),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FontSize = TextFontSize,
            FontWeight = IsTextBold ? FontWeights.Bold : FontWeights.Normal,
            TextAlignment = ToWpfTextAlignment(TextAlignment)
        };

        var resizeThumb = new Thumb
        {
            Width = 13,
            Height = 13,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Cursor = System.Windows.Input.Cursors.SizeNWSE,
            Background = CreateStrokeBrush(),
            Opacity = 0.9
        };

        resizeThumb.DragDelta += (_, args) =>
        {
            container.Width = Math.Max(container.MinWidth, container.Width + args.HorizontalChange);
            container.Height = Math.Max(container.MinHeight, container.Height + args.VerticalChange);
            PositionTextToolbar(container);
            UpdateTextOperation(operationIndex, container, textBox);
        };

        container.Children.Add(textBox);

        if (ShowTextMoveHandles)
        {
            var moveThumb = new Thumb
            {
                Width = 54,
                Height = 13,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Cursor = System.Windows.Input.Cursors.SizeAll,
                Background = CreateStrokeBrush(),
                Opacity = 0.9
            };

            moveThumb.DragDelta += (_, args) =>
            {
                var nextLeft = Math.Clamp(GetLeft(container) + args.HorizontalChange, 0, Math.Max(0, Width - container.Width));
                var nextTop = Math.Clamp(GetTop(container) + args.VerticalChange, 0, Math.Max(0, Height - container.Height));
                SetLeft(container, nextLeft);
                SetTop(container, nextTop);
                PositionTextToolbar(container);
                UpdateTextOperation(operationIndex, container, textBox);
            };

            container.Children.Add(moveThumb);
        }

        container.Children.Add(resizeThumb);
        container.GotKeyboardFocus += (_, _) => SelectTextContainer(container, textBox);
        container.MouseLeftButtonDown += (_, _) => SelectTextContainer(container, textBox);

        SetLeft(container, start.X);
        SetTop(container, start.Y);
        Children.Add(container);
        SelectTextContainer(container, textBox);
        textBox.Focus();
        textBox.SelectAll();

        operations.Add(CreateTextOperation(container, textBox));
        undoEntries.Add(new UndoEntry([container], PreviousBitmapPixels: null));
        textBox.TextChanged += (_, _) => UpdateTextOperation(operationIndex, container, textBox);
    }

    private void UpdateActiveVisuals(WpfPoint start, WpfPoint end)
    {
        switch (ActiveTool)
        {
            case AnnotationTool.Line:
                UpdateLine((Line)activeVisuals![0], end);
                break;
            case AnnotationTool.Arrow:
                UpdateLine((Line)activeVisuals![0], end);
                UpdateArrowHead((Polygon)activeVisuals[1], start, end);
                break;
            case AnnotationTool.Rectangle:
            case AnnotationTool.Pixelate:
                UpdateRectangle((WpfRectangle)activeVisuals![0], start, end);
                break;
            case AnnotationTool.Pen:
                UpdatePen((Polyline)activeVisuals![0], end);
                break;
        }
    }

    private static void UpdateLine(Line line, WpfPoint end)
    {
        line.X2 = end.X;
        line.Y2 = end.Y;
    }

    private static void UpdateRectangle(WpfRectangle rectangle, WpfPoint start, WpfPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);

        SetLeft(rectangle, left);
        SetTop(rectangle, top);
        rectangle.Width = Math.Abs(end.X - start.X);
        rectangle.Height = Math.Abs(end.Y - start.Y);
    }

    private void UpdatePen(Polyline polyline, WpfPoint end)
    {
        polyline.Points.Add(end);
        penPoints.Add(ToPointD(end));
    }

    private void UpdateArrowHead(Polygon head, WpfPoint start, WpfPoint end)
    {
        var vector = start - end;
        if (vector.Length < 0.1)
        {
            head.Points.Clear();
            return;
        }

        vector.Normalize();
        var normal = new Vector(-vector.Y, vector.X);
        var length = Math.Max(12, StrokeThickness * 4);
        var width = Math.Max(7, StrokeThickness * 2.5);

        head.Points = new PointCollection
        {
            end,
            end + (vector * length) + (normal * width),
            end + (vector * length) - (normal * width)
        };
    }

    private void AddOperation(WpfPoint start, WpfPoint end, UIElement[] visuals)
    {
        if (ActiveTool == AnnotationTool.Pixelate)
        {
            AddPixelateOperation(start, end, visuals);
            return;
        }

        var operation = ActiveTool switch
        {
            AnnotationTool.Line => AnnotationOperation.Line(ToPointD(start), ToPointD(end), StrokeColor, StrokeThickness),
            AnnotationTool.Arrow => AnnotationOperation.Arrow(ToPointD(start), ToPointD(end), StrokeColor, StrokeThickness),
            AnnotationTool.Rectangle => AnnotationOperation.Rectangle(ToRectD(start, end), StrokeColor, StrokeThickness),
            AnnotationTool.Pen => AnnotationOperation.Pen(penPoints, StrokeColor, StrokeThickness),
            _ => null
        };

        if (operation is null)
        {
            return;
        }

        operations.Add(operation);
        undoEntries.Add(new UndoEntry(visuals, PreviousBitmapPixels: null));
    }

    private void AddPixelateOperation(WpfPoint start, WpfPoint end, UIElement[] visuals)
    {
        RemoveVisuals(visuals);

        if (baseBitmap is null)
        {
            return;
        }

        var rect = ToRectD(start, end);
        var previousPixels = CopyBitmapPixels();
        ApplyRedaction(rect, ActiveRedactionStyle);

        lastRedactionStyle = ActiveRedactionStyle;
        operations.Add(AnnotationOperation.Redaction(rect, ActiveRedactionStyle));
        undoEntries.Add(new UndoEntry([], previousPixels));
    }

    private void ApplyRedaction(RectD rect, RedactionStyle style)
    {
        switch (style)
        {
            case RedactionStyle.Blackout:
                FillBitmapRect(rect, System.Windows.Media.Color.FromRgb(0, 0, 0));
                break;
            case RedactionStyle.Blur:
                ApplyPixelate(rect, blockSize: 6);
                break;
            default:
                ApplyPixelate(rect, PixelateBlockSize);
                break;
        }
    }

    private void ApplyPixelate(RectD rect, int blockSize)
    {
        if (baseBitmap is null)
        {
            return;
        }

        var width = baseBitmap.PixelWidth;
        var height = baseBitmap.PixelHeight;
        var stride = width * 4;
        var buffer = CopyBitmapPixels();
        var pixels = ToRgbaPixels(buffer);
        var pixelated = PixelateProcessor.Pixelate(
            pixels,
            width,
            height,
            (int)Math.Floor(rect.X),
            (int)Math.Floor(rect.Y),
            (int)Math.Ceiling(rect.Width),
            (int)Math.Ceiling(rect.Height),
            blockSize);

        var pixelatedBytes = ToBgraBytes(pixelated);
        baseBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixelatedBytes, stride, 0);
    }

    private void FillBitmapRect(RectD rect, System.Windows.Media.Color color)
    {
        if (baseBitmap is null)
        {
            return;
        }

        var left = Math.Clamp((int)Math.Floor(rect.X), 0, baseBitmap.PixelWidth);
        var top = Math.Clamp((int)Math.Floor(rect.Y), 0, baseBitmap.PixelHeight);
        var right = Math.Clamp((int)Math.Ceiling(rect.X + rect.Width), 0, baseBitmap.PixelWidth);
        var bottom = Math.Clamp((int)Math.Ceiling(rect.Y + rect.Height), 0, baseBitmap.PixelHeight);
        var width = right - left;
        var height = bottom - top;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = color.B;
            pixels[index + 1] = color.G;
            pixels[index + 2] = color.R;
            pixels[index + 3] = color.A;
        }

        baseBitmap.WritePixels(new Int32Rect(left, top, width, height), pixels, stride, 0);
    }

    private bool IsMeaningfulOperation(WpfPoint start, WpfPoint end)
    {
        return ActiveTool switch
        {
            AnnotationTool.Line or AnnotationTool.Arrow => Distance(start, end) >= MinimumDragDistance,
            AnnotationTool.Rectangle or AnnotationTool.Pixelate => ToRectD(start, end).IsTooSmall(MinimumDragDistance) is false,
            AnnotationTool.Pen => HasMeaningfulPenStroke(),
            _ => true
        };
    }

    private bool HasMeaningfulPenStroke()
    {
        if (penPoints.Count < 2)
        {
            return false;
        }

        var distance = 0d;
        for (var index = 1; index < penPoints.Count; index++)
        {
            distance += Distance(penPoints[index - 1], penPoints[index]);
        }

        return distance >= MinimumDragDistance;
    }

    private void UpdateTextOperation(int operationIndex, WpfGrid container, WpfTextBox textBox)
    {
        if (operationIndex >= operations.Count)
        {
            return;
        }

        var current = operations[operationIndex];
        if (current.Tool != AnnotationTool.Text)
        {
            return;
        }

        operations[operationIndex] = CreateTextOperation(container, textBox);
    }

    private void RemoveVisuals(IEnumerable<UIElement> visuals)
    {
        foreach (var visual in visuals)
        {
            Children.Remove(visual);
        }
    }

    private void SelectTextContainer(WpfGrid container, WpfTextBox textBox)
    {
        selectedTextContainer = container;
        selectedTextBox = textBox;
        ShowTextToolbar(container);
    }

    private void ShowTextToolbar(WpfGrid container)
    {
        if (textToolbar is null)
        {
            textToolbar = CreateTextToolbar();
            Children.Add(textToolbar);
        }

        textToolbar.Visibility = Visibility.Visible;
        PositionTextToolbar(container);
    }

    private Border CreateTextToolbar()
    {
        var panel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal
        };

        panel.Children.Add(CreateTextToolbarButton("A-", DecreaseTextSize));
        panel.Children.Add(CreateTextToolbarButton("A+", IncreaseTextSize));
        panel.Children.Add(CreateTextToolbarButton("B", ToggleTextBold));
        panel.Children.Add(CreateTextToolbarButton("≡", CycleTextAlignment));
        panel.Children.Add(CreateTextToolbarButton("◩", ToggleTextBackground));
        panel.Children.Add(CreateTextToolbarButton("●", CycleTextColor));
        panel.Children.Add(CreateTextToolbarButton("×", DeleteSelectedText));

        return new Border
        {
            Child = panel,
            Padding = new Thickness(5),
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(235, 24, 29, 36)),
            BorderBrush = CreateStrokeBrush(),
            BorderThickness = new Thickness(1)
        };
    }

    private System.Windows.Controls.Button CreateTextToolbarButton(string label, Action action)
    {
        var button = new System.Windows.Controls.Button
        {
            Content = label,
            Width = 32,
            Height = 28,
            Margin = new Thickness(2, 0, 2, 0),
            Padding = new Thickness(0),
            Foreground = WpfBrushes.White,
            Background = WpfBrushes.Transparent,
            BorderBrush = CreateStrokeBrush(),
            BorderThickness = new Thickness(1),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        button.Click += (_, _) => action();
        return button;
    }

    private void PositionTextToolbar(FrameworkElement container)
    {
        if (textToolbar is null)
        {
            return;
        }

        textToolbar.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        SetLeft(textToolbar, Math.Max(0, GetLeft(container)));
        SetTop(textToolbar, Math.Max(0, GetTop(container) - textToolbar.DesiredSize.Height - 8));
    }

    private void DecreaseTextSize() => ChangeSelectedText(textBox => textBox.FontSize = Math.Max(10, textBox.FontSize - 2));

    private void IncreaseTextSize() => ChangeSelectedText(textBox => textBox.FontSize = Math.Min(72, textBox.FontSize + 2));

    private void ToggleTextBold()
    {
        ChangeSelectedText(textBox =>
        {
            textBox.FontWeight = textBox.FontWeight == FontWeights.Bold ? FontWeights.Normal : FontWeights.Bold;
        });
    }

    private void CycleTextAlignment()
    {
        ChangeSelectedText(textBox =>
        {
            textBox.TextAlignment = textBox.TextAlignment switch
            {
                System.Windows.TextAlignment.Left => System.Windows.TextAlignment.Center,
                System.Windows.TextAlignment.Center => System.Windows.TextAlignment.Right,
                _ => System.Windows.TextAlignment.Left
            };
        });
    }

    private void ToggleTextBackground()
    {
        ChangeSelectedText(textBox =>
        {
            textBox.Background = textBox.Background == WpfBrushes.Transparent
                ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 0, 0, 0))
                : WpfBrushes.Transparent;
        });
    }

    private void CycleTextColor()
    {
        ChangeSelectedText(textBox =>
        {
            var current = (textBox.Foreground as SolidColorBrush)?.Color.ToString();
            var next = current switch
            {
                "#FFFF0000" => "#FFFFFFFF",
                "#FFFFFFFF" => "#FF22C55E",
                "#FF22C55E" => "#FFF97316",
                _ => StrokeColor
            };
            textBox.Foreground = (WpfBrush)new BrushConverter().ConvertFromString(next)!;
            textBox.BorderBrush = textBox.Foreground;
        });
    }

    private void DeleteSelectedText()
    {
        if (selectedTextContainer is null)
        {
            return;
        }

        var operationIndex = FindOperationIndex(selectedTextContainer);
        Children.Remove(selectedTextContainer);
        if (operationIndex >= 0)
        {
            undoEntries.RemoveAt(operationIndex);
            operations.RemoveAt(operationIndex);
        }

        if (textToolbar is not null)
        {
            textToolbar.Visibility = Visibility.Collapsed;
        }
    }

    private void ChangeSelectedText(Action<WpfTextBox> update)
    {
        if (selectedTextBox is null || selectedTextContainer is null)
        {
            return;
        }

        update(selectedTextBox);
        var operationIndex = FindOperationIndex(selectedTextContainer);
        if (operationIndex >= 0)
        {
            UpdateTextOperation(operationIndex, selectedTextContainer, selectedTextBox);
        }
    }

    private int FindOperationIndex(UIElement visual)
    {
        for (var index = 0; index < undoEntries.Count; index++)
        {
            if (Array.IndexOf(undoEntries[index].Visuals, visual) >= 0)
            {
                return index;
            }
        }

        return -1;
    }

    private void SetEditingChromeVisibility(Visibility visibility)
    {
        foreach (var child in Children.OfType<WpfGrid>())
        {
            foreach (var thumb in child.Children.OfType<Thumb>())
            {
                thumb.Visibility = visibility;
            }
        }

        if (textToolbar is not null)
        {
            textToolbar.Visibility = visibility;
        }
    }

    private byte[] CopyBitmapPixels()
    {
        if (baseBitmap is null)
        {
            return [];
        }

        var stride = baseBitmap.PixelWidth * 4;
        var pixels = new byte[stride * baseBitmap.PixelHeight];
        baseBitmap.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private void RestoreBitmapPixels(byte[] pixels)
    {
        if (baseBitmap is null)
        {
            return;
        }

        var stride = baseBitmap.PixelWidth * 4;
        baseBitmap.WritePixels(new Int32Rect(0, 0, baseBitmap.PixelWidth, baseBitmap.PixelHeight), pixels, stride, 0);
    }

    private static Rgba32[] ToRgbaPixels(byte[] bgraPixels)
    {
        var pixels = new Rgba32[bgraPixels.Length / 4];

        for (var pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
        {
            var byteIndex = pixelIndex * 4;
            pixels[pixelIndex] = new Rgba32(
                bgraPixels[byteIndex + 2],
                bgraPixels[byteIndex + 1],
                bgraPixels[byteIndex],
                bgraPixels[byteIndex + 3]);
        }

        return pixels;
    }

    private static byte[] ToBgraBytes(IReadOnlyList<Rgba32> rgbaPixels)
    {
        var bytes = new byte[rgbaPixels.Count * 4];

        for (var pixelIndex = 0; pixelIndex < rgbaPixels.Count; pixelIndex++)
        {
            var byteIndex = pixelIndex * 4;
            var pixel = rgbaPixels[pixelIndex];
            bytes[byteIndex] = pixel.B;
            bytes[byteIndex + 1] = pixel.G;
            bytes[byteIndex + 2] = pixel.R;
            bytes[byteIndex + 3] = pixel.A;
        }

        return bytes;
    }

    private static double Distance(WpfPoint start, WpfPoint end)
    {
        var deltaX = end.X - start.X;
        var deltaY = end.Y - start.Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static double Distance(PointD start, PointD end)
    {
        var deltaX = end.X - start.X;
        var deltaY = end.Y - start.Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private WpfBrush CreateStrokeBrush()
    {
        return (WpfBrush)new BrushConverter().ConvertFromString(StrokeColor)!;
    }

    private WpfBrush CreateTextBackgroundBrush()
    {
        return string.IsNullOrWhiteSpace(TextBackgroundColor)
            ? WpfBrushes.Transparent
            : (WpfBrush)new BrushConverter().ConvertFromString(TextBackgroundColor)!;
    }

    private AnnotationOperation CreateTextOperation(WpfGrid container, WpfTextBox textBox)
    {
        return AnnotationOperation.TextLabel(
            ToRectD(container),
            textBox.Text,
            ((SolidColorBrush)textBox.Foreground).Color.ToString(),
            textBox.FontSize,
            textBox.FontWeight == FontWeights.Bold,
            ToTextAnnotationAlignment(textBox.TextAlignment),
            textBox.Background == WpfBrushes.Transparent ? null : ((SolidColorBrush)textBox.Background).Color.ToString());
    }

    private static System.Windows.TextAlignment ToWpfTextAlignment(TextAnnotationAlignment alignment)
    {
        return alignment switch
        {
            TextAnnotationAlignment.Center => System.Windows.TextAlignment.Center,
            TextAnnotationAlignment.Right => System.Windows.TextAlignment.Right,
            _ => System.Windows.TextAlignment.Left
        };
    }

    private static TextAnnotationAlignment ToTextAnnotationAlignment(System.Windows.TextAlignment alignment)
    {
        return alignment switch
        {
            System.Windows.TextAlignment.Center => TextAnnotationAlignment.Center,
            System.Windows.TextAlignment.Right => TextAnnotationAlignment.Right,
            _ => TextAnnotationAlignment.Left
        };
    }

    private static PointD ToPointD(WpfPoint point)
    {
        return new PointD(point.X, point.Y);
    }

    private static RectD ToRectD(WpfPoint start, WpfPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);

        return new RectD(left, top, Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
    }

    private static RectD ToRectD(FrameworkElement element)
    {
        var width = element.ActualWidth > 0 ? element.ActualWidth : element.Width;
        var height = element.ActualHeight > 0 ? element.ActualHeight : element.Height;

        return new RectD(GetLeft(element), GetTop(element), width, height);
    }

    private sealed record UndoEntry(UIElement[] Visuals, byte[]? PreviousBitmapPixels);
}
