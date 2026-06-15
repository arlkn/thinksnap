using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Animation;
using Thinksnap.App.Models;
using Thinksnap.App.Services;

namespace Thinksnap.App;

public partial class UpdateProgressWindow : Window
{
    private static readonly Uri ReleaseNotesUri = new(
        "https://github.com/arlkn/thinksnap/blob/contender-upgrade/docs/ReleaseNotes.md");
    private readonly AppSettings settings;
    private readonly CancellationTokenSource cancellation;
    private TaskCompletionSource<UpdateWindowAction>? pendingAction;

    public UpdateProgressWindow(AppSettings settings, CancellationTokenSource cancellation)
    {
        this.settings = settings;
        this.cancellation = cancellation;
        InitializeComponent();

        Title = T("Update.WindowTitle");
        HeadingText.Text = T("Update.Heading");
        ReleaseNotesButton.Content = T("Update.ReleaseNotes");
        Report(new UpdateProgressState(T("Update.Checking")));
    }

    public Task<UpdateWindowAction> ShowReleaseAsync(UpdateRelease release)
    {
        HeadingText.Text = T("Update.AvailableHeading");
        StatusText.Text = $"{LocalizationService.Format(settings, "Update.AvailableStatus", release.Tag, release.Channel)} " +
            LocalizationService.Format(settings, "Update.Size", FormatBytes(release.Installer.Size));
        DownloadProgressBar.Visibility = Visibility.Visible;
        DownloadProgressBar.Value = 0;
        PercentageText.Visibility = Visibility.Visible;
        PercentageText.Text = "0%";
        ReleaseNotesButton.Visibility = Visibility.Visible;
        return WaitForAction(
            UpdateWindowAction.Download,
            T("Update.Download"),
            UpdateWindowAction.Later,
            T("Update.Later"));
    }

    public Task<UpdateWindowAction> ShowReadyAsync(UpdateRelease release, UpdateVerificationResult verification)
    {
        HeadingText.Text = T("Update.ReadyHeading");
        var securityStatus = verification.SignatureStatus == UpdateSignatureStatus.Unsigned
            ? T("Update.UnsignedWarning")
            : LocalizationService.Format(settings, "Update.SignedBy", verification.Signer ?? "Unknown");
        StatusText.Text = $"{LocalizationService.Format(settings, "Update.ReadyStatus", release.Tag)} {securityStatus}";
        DownloadProgressBar.Visibility = Visibility.Visible;
        DownloadProgressBar.Value = 100;
        PercentageText.Visibility = Visibility.Visible;
        PercentageText.Text = "100%";
        ReleaseNotesButton.Visibility = Visibility.Visible;
        return WaitForAction(
            UpdateWindowAction.Install,
            T("Update.InstallNow"),
            UpdateWindowAction.Later,
            T("Update.Later"));
    }

    public void ShowCompleted(string version)
    {
        HeadingText.Text = T("Update.CompletedHeading");
        StatusText.Text = LocalizationService.Format(settings, "Update.CompletedStatus", version);
        DownloadProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null);
        DownloadProgressBar.Visibility = Visibility.Visible;
        DownloadProgressBar.Value = 100;
        PercentageText.Visibility = Visibility.Visible;
        PercentageText.Text = T("Update.CompletedProgress");
        ReleaseNotesButton.Visibility = Visibility.Collapsed;
        SecondaryButton.Visibility = Visibility.Collapsed;
        CancelButton.Visibility = Visibility.Collapsed;
        PrimaryButton.Tag = UpdateWindowAction.Close;
        PrimaryButton.Content = T("Action.Close");
        PrimaryButton.Visibility = Visibility.Visible;
    }

    public Task<UpdateWindowAction> ShowErrorAsync(string message)
    {
        HeadingText.Text = T("Update.ErrorHeading");
        StatusText.Text = message;
        return WaitForAction(
            UpdateWindowAction.Retry,
            T("Update.Retry"),
            UpdateWindowAction.Cancel,
            T("Action.Cancel"));
    }

    public void ShowProgress()
    {
        ReleaseNotesButton.Visibility = Visibility.Collapsed;
        DownloadProgressBar.Visibility = Visibility.Visible;
        PercentageText.Visibility = Visibility.Visible;
        PrimaryButton.Visibility = Visibility.Collapsed;
        SecondaryButton.Visibility = Visibility.Collapsed;
        CancelButton.Visibility = Visibility.Visible;
    }

    public void Report(UpdateProgressState state)
    {
        StatusText.Text = state.Message;

        if (state.Percentage is null)
        {
            PercentageText.Text = string.Empty;
            var animation = new DoubleAnimation(12, 78, TimeSpan.FromMilliseconds(850))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            DownloadProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, animation);
            return;
        }

        DownloadProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null);
        DownloadProgressBar.Value = state.Percentage.Value;
        PercentageText.Text = $"{state.Percentage.Value:0}%";
        if (state.TotalBytes is > 0)
        {
            PercentageText.Text += $"  {FormatBytes(state.DownloadedBytes)} / {FormatBytes(state.TotalBytes.Value)}";
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        cancellation.Cancel();
        CancelButton.IsEnabled = false;
        StatusText.Text = T("Update.Canceling");
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        pendingAction?.TrySetResult(UpdateWindowAction.Cancel);
        cancellation.Cancel();
    }

    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: UpdateWindowAction action })
        {
            if (action == UpdateWindowAction.Close)
            {
                Close();
                return;
            }

            pendingAction?.TrySetResult(action);
        }
    }

    private void ReleaseNotesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(ReleaseNotesUri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                LocalizationService.Format(settings, "Update.OpenFailed", ex.Message),
                "Thinksnap",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private Task<UpdateWindowAction> WaitForAction(
        UpdateWindowAction primaryAction,
        string primaryText,
        UpdateWindowAction secondaryAction,
        string secondaryText)
    {
        pendingAction = new TaskCompletionSource<UpdateWindowAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        PrimaryButton.Tag = primaryAction;
        PrimaryButton.Content = primaryText;
        PrimaryButton.Visibility = Visibility.Visible;
        SecondaryButton.Tag = secondaryAction;
        SecondaryButton.Content = secondaryText;
        SecondaryButton.Visibility = Visibility.Visible;
        CancelButton.Visibility = Visibility.Collapsed;
        return pendingAction.Task;
    }

    private static string FormatBytes(long bytes) => $"{bytes / 1024d / 1024d:0.0} MB";

    private string T(string key) => LocalizationService.Text(settings, key);
}
