using System.Windows;

namespace Thinksnap.App;

public static class SelectionFrameGeometry
{
    public static Rect MoveWithinBounds(Rect frame, Vector delta, System.Windows.Size bounds)
    {
        var maxLeft = Math.Max(0, bounds.Width - frame.Width);
        var maxTop = Math.Max(0, bounds.Height - frame.Height);
        var nextLeft = Math.Clamp(frame.X + delta.X, 0, maxLeft);
        var nextTop = Math.Clamp(frame.Y + delta.Y, 0, maxTop);

        return new Rect(nextLeft, nextTop, frame.Width, frame.Height);
    }
}
