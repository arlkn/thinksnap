using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using Thinksnap.App.Interop;
using Thinksnap.App.Services;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormsToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace Thinksnap.App;

public partial class MainWindow : Window
{
    private const uint VkSnapshot = 0x2C;

    private readonly CaptureService captureService = new();
    private readonly FormsNotifyIcon trayIcon = new();
    private GlobalHotkey? printScreenHotkey;
    private bool isCapturing;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += MainWindow_Loaded;
        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;
        ConfigureTrayIcon();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Hide();
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
        trayIcon.Visible = false;
        trayIcon.Dispose();
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
            var overlay = new SelectionOverlayWindow(screenCapture, captureService);
            _ = overlay.ShowDialog();

            StatusText.Text = overlay.ResultMessage;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Capture failed: {ex.Message}";
        }
        finally
        {
            isCapturing = false;
        }
    }

    private void ConfigureTrayIcon()
    {
        var menu = new FormsContextMenuStrip();
        var takeScreenshot = new FormsToolStripMenuItem("Take Screenshot");
        takeScreenshot.Click += (_, _) => Dispatcher.Invoke(StartCapture);
        menu.Items.Add(takeScreenshot);

        trayIcon.ContextMenuStrip = menu;
        trayIcon.Icon = SystemIcons.Application;
        trayIcon.Text = "Thinksnap";
        trayIcon.Visible = true;
    }
}
