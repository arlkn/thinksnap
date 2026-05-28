using System.Diagnostics;
using System.Windows;
using Thinksnap.Core.Updates;

namespace Thinksnap.App.Services;

public sealed class UpdateService
{
    public bool OpenUpdatePage(string? updateUrl, Window? owner = null)
    {
        if (UpdateTarget.TryCreateUri(updateUrl, out var uri) is false)
        {
            System.Windows.MessageBox.Show(
                owner,
                "Update URL is not configured yet.",
                "Thinksnap",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                owner,
                $"Could not open update page: {ex.Message}",
                "Thinksnap",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }
}
