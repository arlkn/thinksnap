using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Thinksnap.App.Models;
using Thinksnap.App.Services;

namespace Thinksnap.App;

public partial class FloatingCaptureWindow : Window
{
    private const double DragThreshold = 4;

    private readonly Action captureRequested;
    private readonly Action settingsRequested;
    private readonly AppSettings settings;
    private readonly SettingsService settingsService;
    private System.Windows.Point? dragStart;
    private bool isDragging;

    public FloatingCaptureWindow(Action captureRequested, Action settingsRequested, AppSettings settings, SettingsService settingsService)
    {
        this.captureRequested = captureRequested;
        this.settingsRequested = settingsRequested;
        this.settings = settings;
        this.settingsService = settingsService;
        InitializeComponent();
        ApplyLocalization();

        Loaded += FloatingCaptureWindow_Loaded;
    }

    private void ApplyLocalization()
    {
        CaptureButton.ToolTip = LocalizationService.Text(settings, "Floating.CaptureTooltip");
        TakeScreenshotMenuItem.Header = LocalizationService.Text(settings, "Action.TakeScreenshot");
        OpenSettingsMenuItem.Header = LocalizationService.Text(settings, "Action.Settings");
        HideCaptureButtonMenuItem.Header = LocalizationService.Text(settings, "Floating.Hide");
    }

    private void FloatingCaptureWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyButtonSize();

        if (settings.FloatingCaptureLeft is { } left &&
            settings.FloatingCaptureTop is { } top &&
            IsInsideVirtualScreen(left, top))
        {
            Left = left;
            Top = top;
        }
        else
        {
            Left = SystemParameters.WorkArea.Right - Width - 28;
            Top = SystemParameters.WorkArea.Bottom - Height - 34;
        }

        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
    }

    private void ApplyButtonSize()
    {
        var size = Math.Clamp(settings.CaptureButtonSize, 42, 72);
        Width = size;
        Height = size;
        RootBorder.CornerRadius = new CornerRadius(size / 2);
        CaptureIconImage.Width = size;
        CaptureIconImage.Height = size;
    }

    private void CaptureButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        dragStart = e.GetPosition(this);
        isDragging = false;
        Mouse.Capture((IInputElement)sender);
        e.Handled = true;
    }

    private void CaptureButton_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (dragStart is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        var delta = current - dragStart.Value;
        if (isDragging is false && Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
        {
            return;
        }

        isDragging = true;
        Left += delta.X;
        Top += delta.Y;
        ClampToVirtualScreen();
        e.Handled = true;
    }

    private void CaptureButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Mouse.Capture(null);
        dragStart = null;

        if (isDragging)
        {
            settings.FloatingCaptureLeft = Left;
            settings.FloatingCaptureTop = Top;
            settingsService.Save(settings);
        }
        else
        {
            captureRequested();
        }

        isDragging = false;
        e.Handled = true;
    }

    private void HideCaptureButtonMenuItem_Click(object sender, RoutedEventArgs e)
    {
        settings.ShowFloatingCaptureButton = false;
        settings.FloatingCaptureLeft = Left;
        settings.FloatingCaptureTop = Top;
        settingsService.Save(settings);
        Close();
    }

    private void TakeScreenshotMenuItem_Click(object sender, RoutedEventArgs e)
    {
        captureRequested();
    }

    private void OpenSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        settingsRequested();
    }

    private void ClampToVirtualScreen()
    {
        var minLeft = SystemParameters.VirtualScreenLeft;
        var minTop = SystemParameters.VirtualScreenTop;
        var maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width;
        var maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height;

        Left = Math.Clamp(Left, minLeft, maxLeft);
        Top = Math.Clamp(Top, minTop, maxTop);
    }

    private bool IsInsideVirtualScreen(double left, double top)
    {
        return left >= SystemParameters.VirtualScreenLeft &&
               top >= SystemParameters.VirtualScreenTop &&
               left <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width &&
               top <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height;
    }
}
