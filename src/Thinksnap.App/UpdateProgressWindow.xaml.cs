using System.Windows;
using System.Windows.Media.Animation;
using Thinksnap.App.Models;
using Thinksnap.App.Services;

namespace Thinksnap.App;

public partial class UpdateProgressWindow : Window
{
    private readonly AppSettings settings;

    public UpdateProgressWindow(AppSettings settings)
    {
        this.settings = settings;
        InitializeComponent();

        Title = T("Update.WindowTitle");
        HeadingText.Text = T("Update.Heading");
        Report(new UpdateProgressState(T("Update.Checking")));
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
    }

    private string T(string key) => LocalizationService.Text(settings, key);
}
