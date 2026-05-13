using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using Thinksnap.App.Interop;
using Thinksnap.App.Services;

namespace Thinksnap.App;

public partial class MainWindow : Window
{
    private const uint VkSnapshot = 0x2C;

    private readonly CaptureService captureService = new();
    private GlobalHotkey? printScreenHotkey;
    private bool isCapturing;

    public MainWindow()
    {
        InitializeComponent();

        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            printScreenHotkey = new GlobalHotkey(handle, VkSnapshot, StartCapture);
        }
        catch (Win32Exception)
        {
            StatusText.Text = "PrintScreen hotkey is unavailable.";
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        printScreenHotkey?.Dispose();
        printScreenHotkey = null;
    }

    private void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        StartCapture();
    }

    private async void StartCapture()
    {
        if (isCapturing)
        {
            return;
        }

        isCapturing = true;
        StatusText.Text = "Select a screen region.";

        try
        {
            Hide();
            await Task.Delay(150);

            using var screenCapture = captureService.CaptureVirtualScreen();
            var overlay = new SelectionOverlayWindow();
            var selected = overlay.ShowDialog() == true ? overlay.SelectedRegion : null;

            Show();
            Activate();

            if (selected is null)
            {
                StatusText.Text = "Capture canceled.";
                return;
            }

            using var croppedCapture = captureService.CropBitmap(screenCapture, selected.Value);
            _ = captureService.ToBitmapSource(croppedCapture);
            StatusText.Text = $"Selected {selected.Value.Width}x{selected.Value.Height} region. Editor opens in the next task.";
        }
        catch (Exception ex)
        {
            Show();
            Activate();
            StatusText.Text = $"Capture failed: {ex.Message}";
        }
        finally
        {
            isCapturing = false;
        }
    }
}
