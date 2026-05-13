using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WindowsPoint = System.Windows.Point;

namespace Thinksnap.App;

public partial class SelectionOverlayWindow : Window
{
    private const int MinimumSelectionSize = 5;

    private WindowsPoint? dragStart;

    public SelectionOverlayWindow()
    {
        InitializeComponent();

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    public Int32Rect? SelectedRegion { get; private set; }

    private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        dragStart = e.GetPosition(OverlayCanvas);
        SelectedRegion = null;

        SelectionRectangle.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRectangle, dragStart.Value.X);
        Canvas.SetTop(SelectionRectangle, dragStart.Value.Y);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;

        OverlayCanvas.CaptureMouse();
    }

    private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
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
        var selectedRegion = NormalizeRegion(dragStart.Value, e.GetPosition(OverlayCanvas));
        dragStart = null;

        if (selectedRegion.Width < MinimumSelectionSize || selectedRegion.Height < MinimumSelectionSize)
        {
            SelectedRegion = null;
            DialogResult = false;
            return;
        }

        SelectedRegion = selectedRegion;
        DialogResult = true;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        SelectedRegion = null;
        DialogResult = false;
    }

    private void UpdateSelectionRectangle(WindowsPoint current)
    {
        var region = NormalizeRegion(dragStart!.Value, current);

        Canvas.SetLeft(SelectionRectangle, region.X);
        Canvas.SetTop(SelectionRectangle, region.Y);
        SelectionRectangle.Width = region.Width;
        SelectionRectangle.Height = region.Height;
    }

    private static Int32Rect NormalizeRegion(WindowsPoint start, WindowsPoint end)
    {
        var left = (int)Math.Round(Math.Min(start.X, end.X));
        var top = (int)Math.Round(Math.Min(start.Y, end.Y));
        var right = (int)Math.Round(Math.Max(start.X, end.X));
        var bottom = (int)Math.Round(Math.Max(start.Y, end.Y));

        return new Int32Rect(left, top, right - left, bottom - top);
    }
}
