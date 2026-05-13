using Thinksnap.Core.Annotations;
using Thinksnap.Core.Editing;
using Xunit;

namespace Thinksnap.Core.Tests;

public sealed class EditHistoryTests
{
    [Fact]
    public void UndoRemovesMostRecentAnnotationAndLeavesPreviousOperation()
    {
        var history = new EditHistory();
        var first = AnnotationOperation.Pixelate(new RectD(10, 20, 30, 40));
        var second = AnnotationOperation.Line(
            new PointD(0, 0),
            new PointD(100, 100),
            "#ff0000",
            2);

        history.Add(first);
        history.Add(second);

        var undone = history.Undo();

        Assert.Equal(second, undone);
        var remaining = Assert.Single(history.Operations);
        Assert.Equal(first, remaining);
    }

    [Fact]
    public void UndoOnEmptyHistoryReturnsNullAndLeavesEmptyOperations()
    {
        var history = new EditHistory();

        var undone = history.Undo();

        Assert.Null(undone);
        Assert.Empty(history.Operations);
    }

    [Fact]
    public void OperationsReturnsSnapshotThatCannotMutateHistory()
    {
        var history = new EditHistory();
        var operation = AnnotationOperation.Pixelate(new RectD(10, 20, 30, 40));
        var replacement = AnnotationOperation.Rectangle(new RectD(1, 2, 3, 4), "#00ff00", 2);

        history.Add(operation);

        var snapshot = history.Operations;

        Assert.IsNotType<List<AnnotationOperation>>(snapshot);

        if (snapshot is AnnotationOperation[] snapshotArray)
        {
            snapshotArray[0] = replacement;
        }

        var remaining = Assert.Single(history.Operations);
        Assert.Equal(operation, remaining);
    }

    [Fact]
    public void PixelateOperationStoresSelectedBoundsAndTool()
    {
        var bounds = new RectD(12, 24, 36, 48);

        var operation = AnnotationOperation.Pixelate(bounds);

        Assert.Equal(AnnotationTool.Pixelate, operation.Tool);
        Assert.Equal(bounds, operation.Bounds);
    }

    [Fact]
    public void PenOperationCopiesInputPointsAndStoresImmutablePoints()
    {
        var points = new List<PointD>
        {
            new(1, 2),
            new(3, 4),
        };

        var operation = AnnotationOperation.Pen(points, "#ff0000", 2);

        points[0] = new PointD(9, 9);

        Assert.Equal(new PointD(1, 2), operation.Points[0]);
        Assert.IsNotType<PointD[]>(operation.Points);
    }
}
