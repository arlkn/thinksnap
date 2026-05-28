using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using Thinksnap.App.Models;
using Thinksnap.App.Interop;
using Thinksnap.App.Services;
using Thinksnap.Core.Hotkeys;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsCursor = System.Windows.Forms.Cursor;
using FormsIcon = System.Drawing.Icon;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormsScreen = System.Windows.Forms.Screen;
using FormsSystemInformation = System.Windows.Forms.SystemInformation;
using FormsToolStripSeparator = System.Windows.Forms.ToolStripSeparator;
using FormsToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace Thinksnap.App;

public partial class MainWindow : Window
{
    private readonly CaptureService captureService = new();
    private readonly SettingsService settingsService = new();
    private readonly UpdateService updateService = new();
    private readonly FormsNotifyIcon trayIcon = new();
    private AppSettings appSettings;
    private FloatingCaptureWindow? floatingCaptureWindow;
    private GlobalHotkey? printScreenHotkey;
    private SettingsWindow? settingsWindow;
    private bool isCapturing;
    private bool isExiting;

    public MainWindow()
    {
        InitializeComponent();

        appSettings = settingsService.Load();
        ThemeService.Apply(appSettings);
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;

        _ = new WindowInteropHelper(this).EnsureHandle();
        ConfigureTrayIcon();
        RegisterConfiguredHotkey();
        ShowFloatingCaptureWindow();
    }

    private void RegisterConfiguredHotkey()
    {
        var gesture = HotkeyGesture.ParseOrDefault(appSettings.CaptureHotkey);
        appSettings.CaptureHotkey = gesture.DisplayText;

        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            printScreenHotkey = new GlobalHotkey(handle, gesture, StartCapture);
        }
        catch (Win32Exception ex)
        {
            StatusText.Text = $"{gesture.DisplayText} hotkey is unavailable: {ex.Message}";
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (isExiting)
        {
            return;
        }

        if (appSettings.MinimizeToTrayOnClose is false)
        {
            isExiting = true;
            return;
        }

        e.Cancel = true;
        Hide();
        StatusText.Text = "Thinksnap is still running in the system tray.";
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        printScreenHotkey?.Dispose();
        printScreenHotkey = null;
        trayIcon.Visible = false;
        trayIcon.Dispose();

        if (isExiting)
        {
            System.Windows.Application.Current.Shutdown();
        }
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
        var shouldRestoreWindow = IsVisible;
        StatusText.Text = "Select a screen region.";

        try
        {
            Hide();
            floatingCaptureWindow?.Hide();
            var captureDelay = Math.Clamp(appSettings.CaptureDelayMilliseconds, 0, 1000);
            if (captureDelay > 0)
            {
                await Task.Delay(captureDelay);
            }

            using var screenCapture = captureService.CaptureVirtualScreen();
            var overlay = new SelectionOverlayWindow(screenCapture, captureService, FormsSystemInformation.VirtualScreen, appSettings);
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

            if (shouldRestoreWindow && isExiting is false)
            {
                ShowMainWindow();
            }

            if (isExiting is false)
            {
                SyncFloatingCaptureWindow();
            }
        }
    }

    private void ConfigureTrayIcon()
    {
        var menu = new FormsContextMenuStrip();

        var takeScreenshot = new FormsToolStripMenuItem("Take Screenshot");
        takeScreenshot.Click += (_, _) => Dispatcher.Invoke(StartCapture);
        menu.Items.Add(takeScreenshot);

        var settings = new FormsToolStripMenuItem("Settings...");
        settings.Click += (_, _) => Dispatcher.Invoke(ShowSettings);
        menu.Items.Add(settings);

        var getUpdates = new FormsToolStripMenuItem("Güncelleştirmeleri al...");
        getUpdates.Click += (_, _) => Dispatcher.Invoke(OpenUpdates);
        menu.Items.Add(getUpdates);

        var showCaptureButton = new FormsToolStripMenuItem("Show Capture Button");
        showCaptureButton.Click += (_, _) => Dispatcher.Invoke(ShowCaptureButtonFromTray);
        menu.Items.Add(showCaptureButton);

        menu.Items.Add(new FormsToolStripSeparator());

        var exit = new FormsToolStripMenuItem("Exit");
        exit.Click += (_, _) => Dispatcher.Invoke(ExitApplication);
        menu.Items.Add(exit);

        trayIcon.ContextMenuStrip = menu;
        using var iconStream = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/extension_icon.ico"))?.Stream;
        if (iconStream is not null)
        {
            var trayIconSize = FormsSystemInformation.SmallIconSize;
            using var icon = new FormsIcon(iconStream, trayIconSize);
            trayIcon.Icon = (FormsIcon)icon.Clone();
        }

        trayIcon.Text = "Thinksnap";
        trayIcon.Visible = true;
        trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(StartCapture);
    }

    private void ShowCaptureButtonFromTray()
    {
        var updated = appSettings.Clone();
        updated.ShowFloatingCaptureButton = true;
        _ = TryApplySettings(updated);
    }

    private void OpenUpdates()
    {
        updateService.OpenUpdatePage(appSettings.UpdateUrl, this);
    }

    private void ExitApplication()
    {
        isExiting = true;
        floatingCaptureWindow?.Close();
        settingsWindow?.Close();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private void ShowMainWindow()
    {
        Show();

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    private void ShowSettings()
    {
        if (settingsWindow is { IsVisible: true })
        {
            settingsWindow.Activate();
            return;
        }

        settingsWindow = new SettingsWindow(appSettings.Clone(), TryApplySettings, updateService.OpenUpdatePage)
        {
            Owner = null
        };
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private string? TryApplySettings(AppSettings updatedSettings)
    {
        ArgumentNullException.ThrowIfNull(updatedSettings);

        if (HotkeyGesture.TryParse(updatedSettings.CaptureHotkey, out var gesture) is false)
        {
            return "Unsupported capture hotkey.";
        }

        var previousSettings = appSettings.Clone();
        var previousHotkey = printScreenHotkey;
        GlobalHotkey? replacementHotkey = null;

        try
        {
            if (previousHotkey is null ||
                string.Equals(previousSettings.CaptureHotkey, gesture.DisplayText, StringComparison.OrdinalIgnoreCase) is false)
            {
                var handle = new WindowInteropHelper(this).Handle;
                replacementHotkey = new GlobalHotkey(handle, gesture, StartCapture);
            }

            updatedSettings.CaptureHotkey = gesture.DisplayText;
            appSettings = updatedSettings.Clone();
            settingsService.Save(appSettings);
            ThemeService.Apply(appSettings);

            if (replacementHotkey is not null)
            {
                printScreenHotkey = replacementHotkey;
                replacementHotkey = null;
                previousHotkey?.Dispose();
            }

            SyncFloatingCaptureWindow();
            return null;
        }
        catch (Win32Exception ex)
        {
            replacementHotkey?.Dispose();
            appSettings = previousSettings;
            return $"{gesture.DisplayText} is unavailable: {ex.Message}";
        }
    }

    private void ShowFloatingCaptureWindow()
    {
        if (appSettings.ShowFloatingCaptureButton is false)
        {
            HideFloatingCaptureWindow();
            return;
        }

        if (floatingCaptureWindow is { IsVisible: true })
        {
            return;
        }

        if (floatingCaptureWindow is null)
        {
            floatingCaptureWindow = new FloatingCaptureWindow(StartCapture, ShowSettings, appSettings, settingsService);
            floatingCaptureWindow.Closed += (_, _) => floatingCaptureWindow = null;
        }

        floatingCaptureWindow.Show();
    }

    private void HideFloatingCaptureWindow()
    {
        floatingCaptureWindow?.Close();
        floatingCaptureWindow = null;
    }

    private void SyncFloatingCaptureWindow()
    {
        HideFloatingCaptureWindow();
        if (appSettings.ShowFloatingCaptureButton)
        {
            ShowFloatingCaptureWindow();
        }
    }
}
