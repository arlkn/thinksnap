using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Thinksnap.Core.Annotations;
using WpfPoint = System.Windows.Point;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace Thinksnap.App.Controls;

public sealed class AnnotationCanvas : Canvas
{
    private const string DefaultStroke = "#ff0000";
    private const double DefaultStrokeThickness = 3;

    private readonly Image baseImage = new();
    private readonly List<AnnotationOperation> operations = [];
    private readonly List<UIElement[]> operationVisuals = [];
    private readonly List<PointD> penPoints = [];

    private WpfPoint? dragStart;
    private UIElement[]? activeVisuals;

    public AnnotationCanvas()
    {
        Background = Brushes.Transparent;
        ClipToBounds = true;
        Children.Add(baseImage);
    }

    public AnnotationTool ActiveTool { get; set; } = AnnotationTool.Rectangle;

    public string StrokeColor { get; set; } = DefaultStroke;

    public double StrokeThickness { get; set; } = DefaultStrokeThickness;

    public IReadOnlyList<AnnotationOperation> Operations => operations.ToArray();

    public void SetImage(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        baseImage.Source = source;
        baseImage.Width = source.Width;
        baseImage.Height = source.Height;
        Width = source.Width;
        Height = source.Height;
    }

    public void Undo()
    {
        if (operationVisuals.Count == 0)
        {
            return;
        }

        var visuals = operationVisuals[^1];
        operationVisuals.RemoveAt(operationVisuals.Count - 1);

        foreach (var visual in visuals)
        {
            Children.Remove(visual);
        }

        if (operations.Count > 0)
        {
            operations.RemoveAt(operations.Count - 1);
        }
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

    protected override void OnMouseMove(MouseEventArgs e)
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
        AddOperation(start, end, activeVisuals);

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
            AnnotationTool.Rectangle => AddRectangle(start, fill: Brushes.Transparent),
            AnnotationTool.Pixelate => AddRectangle(start, fill: CreatePixelateFill()),
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

    private UIElement[] AddRectangle(WpfPoint start, Brush fill)
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

    private void AddText(WpfPoint start)
    {
        var textBox = new TextBox
        {
            Text = "Text",
            Foreground = CreateStrokeBrush(),
            Background = Brushes.Transparent,
            BorderBrush = CreateStrokeBrush(),
            BorderThickness = new Thickness(1),
            MinWidth = 80
        };

        SetLeft(textBox, start.X);
        SetTop(textBox, start.Y);
        Children.Add(textBox);
        textBox.Focus();
        textBox.SelectAll();

        operations.Add(AnnotationOperation.TextLabel(ToPointD(start), textBox.Text, StrokeColor));
        operationVisuals.Add([textBox]);
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
        var operation = ActiveTool switch
        {
            AnnotationTool.Line => AnnotationOperation.Line(ToPointD(start), ToPointD(end), StrokeColor, StrokeThickness),
            AnnotationTool.Arrow => AnnotationOperation.Arrow(ToPointD(start), ToPointD(end), StrokeColor, StrokeThickness),
            AnnotationTool.Rectangle => AnnotationOperation.Rectangle(ToRectD(start, end), StrokeColor, StrokeThickness),
            AnnotationTool.Pixelate => AnnotationOperation.Pixelate(ToRectD(start, end)),
            AnnotationTool.Pen => AnnotationOperation.Pen(penPoints, StrokeColor, StrokeThickness),
            _ => null
        };

        if (operation is null)
        {
            return;
        }

        operations.Add(operation);
        operationVisuals.Add(visuals);
    }

    private Brush CreateStrokeBrush()
    {
        return (Brush)new BrushConverter().ConvertFromString(StrokeColor)!;
    }

    private static Brush CreatePixelateFill()
    {
        return new SolidColorBrush(Color.FromArgb(80, 255, 0, 0));
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
}
