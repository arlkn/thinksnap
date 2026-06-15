using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using Thinksnap.App.Models;
using Thinksnap.App.Services;

namespace Thinksnap.App;

public partial class UpdateProgressWindow : Window
{
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
        Report(new UpdateProgressState(T("Update.Checking")));
    }

    public Task<UpdateWindowAction> ShowReleaseAsync(UpdateRelease release)
    {
        HeadingText.Text = T("Update.AvailableHeading");
        StatusText.Text = LocalizationService.Format(settings, "Update.AvailableStatus", release.Tag, release.Channel);
        DetailsText.Text = $"{LocalizationService.Format(settings, "Update.Size", FormatBytes(release.Installer.Size))}\n\n{release.Notes}";
        DetailsText.Visibility = Visibility.Visible;
        DownloadProgressBar.Visibility = Visibility.Collapsed;
        PercentageText.Visibility = Visibility.Collapsed;
        return WaitForAction(
            UpdateWindowAction.Download,
            T("Update.Download"),
            UpdateWindowAction.Later,
            T("Update.Later"));
    }

    public Task<UpdateWindowAction> ShowReadyAsync(UpdateRelease release, UpdateVerificationResult verification)
    {
        HeadingText.Text = T("Update.ReadyHeading");
        StatusText.Text = LocalizationService.Format(settings, "Update.ReadyStatus", release.Tag);
        DetailsText.Text = verification.SignatureStatus == UpdateSignatureStatus.Unsigned
            ? T("Update.UnsignedWarning")
            : LocalizationService.Format(settings, "Update.SignedBy", verification.Signer ?? "Unknown");
        DetailsText.Visibility = Visibility.Visible;
        DownloadProgressBar.Visibility = Visibility.Visible;
        DownloadProgressBar.Value = 100;
        PercentageText.Visibility = Visibility.Visible;
        PercentageText.Text = "100%";
        return WaitForAction(
            UpdateWindowAction.Install,
            T("Update.InstallNow"),
            UpdateWindowAction.Later,
            T("Update.Later"));
    }

    public Task<UpdateWindowAction> ShowErrorAsync(string message)
    {
        HeadingText.Text = T("Update.ErrorHeading");
        StatusText.Text = message;
        DetailsText.Visibility = Visibility.Collapsed;
        return WaitForAction(
            UpdateWindowAction.Retry,
            T("Update.Retry"),
            UpdateWindowAction.Cancel,
            T("Action.Cancel"));
    }

    public void ShowProgress()
    {
        DetailsText.Visibility = Visibility.Collapsed;
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
            pendingAction?.TrySetResult(action);
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
