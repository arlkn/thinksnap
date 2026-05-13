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
    public void PixelateOperationStoresSelectedBoundsAndTool()
    {
        var bounds = new RectD(12, 24, 36, 48);

        var operation = AnnotationOperation.Pixelate(bounds);

        Assert.Equal(AnnotationTool.Pixelate, operation.Tool);
        Assert.Equal(bounds, operation.Bounds);
    }
}
