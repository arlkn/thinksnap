using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using Thinksnap.App.Models;
using Thinksnap.App.Interop;
using Thinksnap.App.Services;
using Thinksnap.Core.Hotkeys;
using Thinksnap.Core.Updates;
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
    private UpdateProgressWindow? updateCompletionWindow;
    private bool isCapturing;
    private bool isExiting;

    public MainWindow()
    {
        InitializeComponent();

        appSettings = settingsService.Load();
        appSettings.Language = LocalizationService.NormalizeLanguage(appSettings.Language);
        ThemeService.Apply(appSettings);
        ApplyLocalization();
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;

        _ = new WindowInteropHelper(this).EnsureHandle();
        var completedUpdateVersion = updateService.ConsumeCompletedUpdate();
        ConfigureTrayIcon();
        RegisterConfiguredHotkey();
        ShowFloatingCaptureWindow();
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            if (completedUpdateVersion is not null)
            {
                ShowCompletedUpdate(completedUpdateVersion);
                return;
            }

            _ = CheckForUpdatesSilentlyAsync();
        }));
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
            StatusText.Text = L("Main.HotkeyUnavailable", gesture.DisplayText, ex.Message);
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
        StatusText.Text = T("Main.MinimizedToTray");
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
        StatusText.Text = T("Capture.SelectRegion");

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
            var overlay = new SelectionOverlayWindow(screenCapture, captureService, GetVirtualOverlayBounds(), appSettings);
            _ = overlay.ShowDialog();

            StatusText.Text = overlay.ResultMessage;
        }
        catch (Exception ex)
        {
            StatusText.Text = L("Capture.Failed", ex.Message);
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

    private static Rect GetVirtualOverlayBounds()
    {
        return new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
    }

    private void ConfigureTrayIcon()
    {
        var menu = new FormsContextMenuStrip();

        var takeScreenshot = new FormsToolStripMenuItem(T("Action.TakeScreenshot"));
        takeScreenshot.Click += (_, _) => Dispatcher.Invoke(StartCapture);
        menu.Items.Add(takeScreenshot);

        var settings = new FormsToolStripMenuItem(T("Action.Settings"));
        settings.Click += (_, _) => Dispatcher.Invoke(ShowSettings);
        menu.Items.Add(settings);

        var getUpdates = new FormsToolStripMenuItem(T("Action.Updates"));
        getUpdates.Click += (_, _) => Dispatcher.Invoke(OpenUpdates);
        menu.Items.Add(getUpdates);

        var readyState = updateService.DownloadState;
        if (readyState.Verification?.IsValid == true &&
            string.IsNullOrWhiteSpace(readyState.ReadyInstallerPath) is false &&
            File.Exists(readyState.ReadyInstallerPath))
        {
            var installReadyUpdate = new FormsToolStripMenuItem(T("Tray.InstallReadyUpdate"));
            installReadyUpdate.Click += (_, _) => Dispatcher.Invoke(() => updateService.InstallReadyUpdate());
            menu.Items.Add(installReadyUpdate);
        }

        var showCaptureButton = new FormsToolStripMenuItem(T("Tray.ShowCaptureButton"));
        showCaptureButton.Click += (_, _) => Dispatcher.Invoke(ShowCaptureButtonFromTray);
        menu.Items.Add(showCaptureButton);

        menu.Items.Add(new FormsToolStripSeparator());

        var exit = new FormsToolStripMenuItem(T("Tray.Exit"));
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
        trayIcon.DoubleClick -= TrayIcon_DoubleClick;
        trayIcon.DoubleClick += TrayIcon_DoubleClick;
        trayIcon.BalloonTipClicked -= TrayIcon_BalloonTipClicked;
        trayIcon.BalloonTipClicked += TrayIcon_BalloonTipClicked;
    }

    private void TrayIcon_DoubleClick(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(StartCapture);
    }

    private void TrayIcon_BalloonTipClicked(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(OpenUpdates);
    }

    private void ShowCaptureButtonFromTray()
    {
        var updated = appSettings.Clone();
        updated.ShowFloatingCaptureButton = true;
        _ = TryApplySettings(updated);
    }

    private async void OpenUpdates()
    {
        StatusText.Text = T("Update.Checking");
        var installerStarted = await updateService.CheckAndInstallLatestAsync(appSettings.UpdateUrl, this, appSettings);
        settingsService.Save(appSettings);
        ConfigureTrayIcon();
        if (installerStarted)
        {
            StatusText.Text = T("Update.StartingInstaller");
        }
    }

    private async Task CheckForUpdatesSilentlyAsync()
    {
        if (appSettings.AutomaticallyCheckForUpdates is false ||
            UpdateCheckSchedule.IsDue(appSettings.LastUpdateCheckUtc, DateTimeOffset.UtcNow) is false)
        {
            return;
        }

        try
        {
            var release = await updateService.CheckForUpdateAsync(appSettings.UpdateChannel);
            appSettings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            settingsService.Save(appSettings);
            if (release is null)
            {
                return;
            }

            trayIcon.BalloonTipTitle = T("Update.NotificationTitle");
            trayIcon.BalloonTipText = L("Update.NotificationBody", release.Tag);
            trayIcon.ShowBalloonTip(7000);
        }
        catch
        {
            // Silent checks must never interfere with capture startup.
        }
    }

    private void ShowCompletedUpdate(string version)
    {
        if (updateCompletionWindow is { IsVisible: true })
        {
            updateCompletionWindow.Activate();
            return;
        }

        var cancellation = new CancellationTokenSource();
        updateCompletionWindow = new UpdateProgressWindow(appSettings, cancellation)
        {
            Owner = null,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        updateCompletionWindow.ShowCompleted(version);
        updateCompletionWindow.Closed += (_, _) =>
        {
            cancellation.Dispose();
            updateCompletionWindow = null;
        };
        updateCompletionWindow.Show();
        updateCompletionWindow.Activate();
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

        settingsWindow = new SettingsWindow(appSettings.Clone(), TryApplySettings, CheckForUpdatesFromSettingsAsync)
        {
            Owner = null
        };
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private async Task<bool> CheckForUpdatesFromSettingsAsync(string? updateUrl, Window? owner)
    {
        var started = await updateService.CheckAndInstallLatestAsync(updateUrl, owner, appSettings);
        settingsService.Save(appSettings);
        ConfigureTrayIcon();
        return started;
    }

    private string? TryApplySettings(AppSettings updatedSettings)
    {
        ArgumentNullException.ThrowIfNull(updatedSettings);

        if (HotkeyGesture.TryParse(updatedSettings.CaptureHotkey, out var gesture) is false)
        {
            return T("Settings.UnsupportedHotkey");
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
            updatedSettings.Language = LocalizationService.NormalizeLanguage(updatedSettings.Language);
            appSettings = updatedSettings.Clone();
            settingsService.Save(appSettings);
            ThemeService.Apply(appSettings);
            ApplyLocalization();

            if (replacementHotkey is not null)
            {
                printScreenHotkey = replacementHotkey;
                replacementHotkey = null;
                previousHotkey?.Dispose();
            }

            SyncFloatingCaptureWindow();
            ConfigureTrayIcon();
            return null;
        }
        catch (Win32Exception ex)
        {
            replacementHotkey?.Dispose();
            appSettings = previousSettings;
            return L("Main.HotkeyUnavailable", gesture.DisplayText, ex.Message);
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

    private void ApplyLocalization()
    {
        LauncherSubtitleText.Text = T("Main.LauncherSubtitle");
        if (string.IsNullOrWhiteSpace(StatusText.Text) ||
            string.Equals(StatusText.Text, "Use the floating capture icon, tray, or PrintScreen.", StringComparison.Ordinal))
        {
            StatusText.Text = T("Main.LauncherStatus");
        }

        CaptureButton.Content = T("Floating.CaptureTooltip");
    }

    private string T(string key) => LocalizationService.Text(appSettings, key);

    private string L(string key, params object[] values) => LocalizationService.Format(appSettings, key, values);
}
