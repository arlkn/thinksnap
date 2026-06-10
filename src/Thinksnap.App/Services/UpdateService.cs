using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Thinksnap.App.Models;
using Thinksnap.Core.Updates;

namespace Thinksnap.App.Services;

public sealed class UpdateService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> CheckAndInstallLatestAsync(string? updateUrl, Window? owner = null, AppSettings? settings = null)
    {
        var activeSettings = settings ?? new AppSettings();
        var progressWindow = new UpdateProgressWindow(activeSettings);
        if (owner?.IsVisible == true)
        {
            progressWindow.Owner = owner;
            progressWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        progressWindow.Show();
        var progress = new Progress<UpdateProgressState>(progressWindow.Report);

        try
        {
            return await CheckAndInstallLatestCoreAsync(updateUrl, progressWindow, activeSettings, progress);
        }
        finally
        {
            progressWindow.Close();
        }
    }

    private static async Task<bool> CheckAndInstallLatestCoreAsync(
        string? updateUrl,
        Window owner,
        AppSettings activeSettings,
        IProgress<UpdateProgressState> progress)
    {
        progress.Report(new UpdateProgressState(LocalizationService.Text(activeSettings, "Update.Checking")));

        if (UpdateTarget.TryCreateUri(updateUrl, out var releasePageUri) is false)
        {
            Show(owner, activeSettings, "Update.NotConfigured", MessageBoxImage.Information);
            return false;
        }

        if (TryCreateLatestReleaseApiUri(releasePageUri, out var apiUri) is false)
        {
            Show(owner, activeSettings, "Update.UnableToBuildApiUrl", MessageBoxImage.Error);
            return false;
        }

        GitHubRelease release;
        try
        {
            await using var releaseStream = await HttpClient.GetStreamAsync(apiUri);
            release = await JsonSerializer.DeserializeAsync<GitHubRelease>(releaseStream, JsonOptions) ??
                throw new InvalidOperationException("Empty GitHub release response.");
        }
        catch (Exception ex)
        {
            Show(owner, activeSettings, "Update.ReleaseReadFailed", MessageBoxImage.Error, ex.Message);
            return false;
        }

        var currentVersion = GetCurrentVersion();
        var latestVersion = ParseVersion(release.TagName);
        if (latestVersion is not null && latestVersion.CompareTo(currentVersion) <= 0)
        {
            Show(owner, activeSettings, "Update.NoUpdates", MessageBoxImage.Information, currentVersion);
            return false;
        }

        var asset = SelectInstallerAsset(release.Assets ?? Array.Empty<GitHubAsset>());
        if (asset is null || Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var downloadUri) is false)
        {
            Show(owner, activeSettings, "Update.NoInstallerAsset", MessageBoxImage.Information);
            return false;
        }

        string installerPath;
        try
        {
            progress.Report(new UpdateProgressState(
                LocalizationService.Format(activeSettings, "Update.Downloading", release.TagName),
                0));
            installerPath = await DownloadInstallerAsync(downloadUri, asset.Name, release.TagName, progress, activeSettings);
        }
        catch (Exception ex)
        {
            Show(owner, activeSettings, "Update.DownloadFailed", MessageBoxImage.Error, ex.Message);
            return false;
        }

        try
        {
            progress.Report(new UpdateProgressState(
                LocalizationService.Text(activeSettings, "Update.StartingInstaller"),
                100));
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
                UseShellExecute = true
            });
            await Task.Delay(350);
            _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(
                new Action(System.Windows.Application.Current.Shutdown));
            return true;
        }
        catch (Exception ex)
        {
            Show(owner, activeSettings, "Update.InstallStartFailed", MessageBoxImage.Error, ex.Message);
            return false;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Thinksnap-Updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.Timeout = TimeSpan.FromSeconds(45);
        return client;
    }

    private static bool TryCreateLatestReleaseApiUri(Uri releasePageUri, out Uri apiUri)
    {
        apiUri = new Uri(UpdateTarget.DefaultUrl);
        if (string.Equals(releasePageUri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase))
        {
            apiUri = releasePageUri;
            return true;
        }

        if (string.Equals(releasePageUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) is false)
        {
            return false;
        }

        var segments = releasePageUri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        apiUri = new Uri($"https://api.github.com/repos/{segments[0]}/{segments[1]}/releases/latest");
        return true;
    }

    private static Version GetCurrentVersion()
    {
        var assembly = typeof(UpdateService).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return ParseVersion(informationalVersion) ?? assembly.GetName().Version ?? new Version(0, 0, 0);
    }

    private static Version? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var metadataIndex = normalized.IndexOfAny(new[] { '+', '-' });
        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }

        return Version.TryParse(normalized, out var version) ? version : null;
    }

    private static GitHubAsset? SelectInstallerAsset(IReadOnlyList<GitHubAsset> assets)
    {
        return assets
            .Where(asset => asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(asset => asset.Name.Contains("setup", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(asset => asset.Name.Contains("thinksnap", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    private static async Task<string> DownloadInstallerAsync(
        Uri downloadUri,
        string assetName,
        string tagName,
        IProgress<UpdateProgressState> progress,
        AppSettings settings)
    {
        var safeTag = string.Join("_", tagName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var updateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Thinksnap",
            "Updates",
            safeTag);
        Directory.CreateDirectory(updateDirectory);

        var installerPath = Path.Combine(updateDirectory, assetName);
        using var response = await HttpClient.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync();
        await using var destination = File.Create(installerPath);
        var totalBytes = response.Content.Headers.ContentLength;
        var buffer = new byte[81920];
        long downloadedBytes = 0;

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer);
            if (bytesRead == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead));
            downloadedBytes += bytesRead;
            var percentage = UpdateProgressCalculator.CalculatePercentage(downloadedBytes, totalBytes);
            progress.Report(new UpdateProgressState(
                LocalizationService.Format(settings, "Update.Downloading", tagName),
                percentage));
        }

        return installerPath;
    }

    private static void Show(Window? owner, AppSettings settings, string key, MessageBoxImage image, params object[] args)
    {
        var message = args.Length == 0
            ? LocalizationService.Text(settings, key)
            : LocalizationService.Format(settings, key, args);
        System.Windows.MessageBox.Show(owner, message, "Thinksnap", MessageBoxButton.OK, image);
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("assets")] IReadOnlyList<GitHubAsset> Assets);

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
