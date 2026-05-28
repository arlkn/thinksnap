using System.Collections.Immutable;

namespace Thinksnap.Core.Annotations;

public sealed record AnnotationOperation
{
    private AnnotationOperation(
        AnnotationTool tool,
        RectD? bounds,
        PointD? start,
        PointD? end,
        ImmutableArray<PointD> points,
        string? text,
        string color,
        double strokeThickness,
        RedactionStyle? redactionStyle,
        double fontSize,
        bool isBold,
        TextAnnotationAlignment textAlignment,
        string? backgroundColor)
    {
        Tool = tool;
        Bounds = bounds;
        Start = start;
        End = end;
        Points = points;
        Text = text;
        Color = color;
        StrokeThickness = strokeThickness;
        RedactionStyle = redactionStyle;
        FontSize = fontSize;
        IsBold = isBold;
        TextAlignment = textAlignment;
        BackgroundColor = backgroundColor;
    }

    public AnnotationTool Tool { get; }

    public RectD? Bounds { get; }

    public PointD? Start { get; }

    public PointD? End { get; }

    public ImmutableArray<PointD> Points { get; }

    public string? Text { get; }

    public string Color { get; }

    public double StrokeThickness { get; }

    public RedactionStyle? RedactionStyle { get; }

    public double FontSize { get; }

    public bool IsBold { get; }

    public TextAnnotationAlignment TextAlignment { get; }

    public string? BackgroundColor { get; }

    public static AnnotationOperation Pixelate(RectD bounds)
    {
        return Redaction(bounds, Annotations.RedactionStyle.Pixelate);
    }

    public static AnnotationOperation Redaction(RectD bounds, RedactionStyle redactionStyle)
    {
        return new AnnotationOperation(
            AnnotationTool.Pixelate,
            bounds,
            null,
            null,
            ImmutableArray<PointD>.Empty,
            null,
            "#ff0000",
            0,
            redactionStyle,
            0,
            false,
            TextAnnotationAlignment.Left,
            null);
    }

    public static AnnotationOperation Arrow(PointD start, PointD end, string color, double strokeThickness)
    {
        return new AnnotationOperation(
            AnnotationTool.Arrow,
            null,
            start,
            end,
            ImmutableArray<PointD>.Empty,
            null,
            color,
            strokeThickness,
            null,
            0,
            false,
            TextAnnotationAlignment.Left,
            null);
    }

    public static AnnotationOperation Line(PointD start, PointD end, string color, double strokeThickness)
    {
        return new AnnotationOperation(
            AnnotationTool.Line,
            null,
            start,
            end,
            ImmutableArray<PointD>.Empty,
            null,
            color,
            strokeThickness,
            null,
            0,
            false,
            TextAnnotationAlignment.Left,
            null);
    }

    public static AnnotationOperation Rectangle(RectD bounds, string color, double strokeThickness)
    {
        return new AnnotationOperation(
            AnnotationTool.Rectangle,
            bounds,
            null,
            null,
            ImmutableArray<PointD>.Empty,
            null,
            color,
            strokeThickness,
            null,
            0,
            false,
            TextAnnotationAlignment.Left,
            null);
    }

    public static AnnotationOperation Pen(IReadOnlyList<PointD> points, string color, double strokeThickness)
    {
        ArgumentNullException.ThrowIfNull(points);

        return new AnnotationOperation(
            AnnotationTool.Pen,
            null,
            null,
            null,
            points.ToImmutableArray(),
            null,
            color,
            strokeThickness,
            null,
            0,
            false,
            TextAnnotationAlignment.Left,
            null);
    }

    public static AnnotationOperation TextLabel(PointD start, string text, string color)
    {
        return TextLabel(new RectD(start.X, start.Y, 0, 0), text, color);
    }

    public static AnnotationOperation TextLabel(
        RectD bounds,
        string text,
        string color,
        double fontSize = 18,
        bool isBold = false,
        TextAnnotationAlignment textAlignment = TextAnnotationAlignment.Left,
        string? backgroundColor = null)
    {
        return new AnnotationOperation(
            AnnotationTool.Text,
            bounds,
            new PointD(bounds.X, bounds.Y),
            null,
            ImmutableArray<PointD>.Empty,
            text,
            color,
            0,
            null,
            fontSize,
            isBold,
            textAlignment,
            backgroundColor);
    }
}
