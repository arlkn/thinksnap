namespace Thinksnap.Core.Annotations;

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public bool IsTooSmall(double minimumSize)
    {
        return Width < minimumSize || Height < minimumSize;
    }
}
