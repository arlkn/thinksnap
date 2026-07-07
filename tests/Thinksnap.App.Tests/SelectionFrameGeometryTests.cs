using System.Windows;
using Thinksnap.App;
using Xunit;

namespace Thinksnap.App.Tests;

public sealed class SelectionFrameGeometryTests
{
    [Fact]
    public void MoveWithinBoundsKeepsFrameInsideViewport()
    {
        var frame = new Rect(80, 40, 120, 90);
        var viewport = new Size(300, 180);

        var moved = SelectionFrameGeometry.MoveWithinBounds(frame, new Vector(260, 200), viewport);

        Assert.Equal(new Rect(180, 90, 120, 90), moved);
    }

    [Fact]
    public void MoveWithinBoundsAllowsNormalDragDelta()
    {
        var frame = new Rect(80, 40, 120, 90);
        var viewport = new Size(300, 180);

        var moved = SelectionFrameGeometry.MoveWithinBounds(frame, new Vector(15, -20), viewport);

        Assert.Equal(new Rect(95, 20, 120, 90), moved);
    }
}
