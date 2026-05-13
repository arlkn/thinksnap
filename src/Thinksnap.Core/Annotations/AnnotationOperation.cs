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
        double strokeThickness)
    {
        Tool = tool;
        Bounds = bounds;
        Start = start;
        End = end;
        Points = points;
        Text = text;
        Color = color;
        StrokeThickness = strokeThickness;
    }

    public AnnotationTool Tool { get; }

    public RectD? Bounds { get; }

    public PointD? Start { get; }

    public PointD? End { get; }

    public ImmutableArray<PointD> Points { get; }

    public string? Text { get; }

    public string Color { get; }

    public double StrokeThickness { get; }

    public static AnnotationOperation Pixelate(RectD bounds)
    {
        return new AnnotationOperation(
            AnnotationTool.Pixelate,
            bounds,
            null,
            null,
            ImmutableArray<PointD>.Empty,
            null,
            "#ff0000",
            0);
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
            strokeThickness);
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
            strokeThickness);
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
            strokeThickness);
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
            strokeThickness);
    }

    public static AnnotationOperation TextLabel(PointD start, string text, string color)
    {
        return new AnnotationOperation(
            AnnotationTool.Text,
            null,
            start,
            null,
            ImmutableArray<PointD>.Empty,
            text,
            color,
            0);
    }
}
