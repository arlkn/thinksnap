using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
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
    private readonly UpdateStateService stateService = new();

    public UpdateDownloadState DownloadState => stateService.Load();

    public string? ConsumeCompletedUpdate()
    {
        var state = stateService.Load();
        if (state.InstallPending is false ||
            UpdateInstallCompletionPolicy.IsCompleted(state.Version, GetCurrentVersion().ToString()) is false)
        {
            return null;
        }

        var completedVersion = state.Version?.TrimStart('v', 'V');
        stateService.Clear();
        return completedVersion;
    }

    public async Task<UpdateRelease?> CheckForUpdateAsync(UpdateChannel channel, CancellationToken cancellationToken = default)
    {
        var releases = await ReadReleasesAsync(cancellationToken);
        var selected = UpdateReleaseSelector.SelectLatest(
            releases.Where(item => item.Draft is false).Select(item => new UpdateCandidate(item.TagName, item.Prerelease)),
            channel);
        if (selected is null ||
            SemanticVersion.TryParse(selected.Tag, out var selectedVersion) is false ||
            selectedVersion.CompareTo(GetCurrentVersion()) <= 0)
        {
            return null;
        }

        var release = releases.First(item => string.Equals(item.TagName, selected.Tag, StringComparison.OrdinalIgnoreCase));
        var installer = SelectAsset(release.Assets, ".exe", "setup");
        var checksum = SelectAsset(release.Assets, ".sha256.txt", "setup");
        if (installer is null || checksum is null ||
            Uri.TryCreate(installer.BrowserDownloadUrl, UriKind.Absolute, out var installerUri) is false ||
            Uri.TryCreate(checksum.BrowserDownloadUrl, UriKind.Absolute, out var checksumUri) is false ||
            Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out var releaseUri) is false)
        {
            throw new InvalidOperationException("The release does not contain the required installer and SHA-256 assets.");
        }

        return new UpdateRelease(
            release.TagName,
            selectedVersion,
            release.Prerelease ? UpdateChannel.Beta : UpdateChannel.Stable,
            release.Body ?? string.Empty,
            releaseUri,
            ToAsset(installer, installerUri),
            ToAsset(checksum, checksumUri));
    }

    public async Task<bool> CheckAndInstallLatestAsync(string? updateUrl, Window? owner = null, AppSettings? settings = null)
    {
        var activeSettings = settings ?? new AppSettings();
        using var cancellation = new CancellationTokenSource();
        var progressWindow = new UpdateProgressWindow(activeSettings, cancellation)
        {
            Owner = owner?.IsVisible == true ? owner : null,
            WindowStartupLocation = owner?.IsVisible == true ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
        };
        progressWindow.Show();

        try
        {
            progressWindow.Report(new UpdateProgressState(LocalizationService.Text(activeSettings, "Update.Checking")));
            var release = await CheckForUpdateAsync(activeSettings.UpdateChannel, cancellation.Token);
            activeSettings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            if (release is null)
            {
                progressWindow.Close();
                Show(owner, activeSettings, "Update.NoUpdates", MessageBoxImage.Information, GetCurrentVersion());
                return false;
            }

            if (await progressWindow.ShowReleaseAsync(release) != UpdateWindowAction.Download)
            {
                return false;
            }

            UpdateVerificationResult verification;
            while (true)
            {
                try
                {
                    progressWindow.ShowProgress();
                    verification = await DownloadAndVerifyAsync(
                        release,
                        new Progress<UpdateProgressState>(progressWindow.Report),
                        cancellation.Token,
                        activeSettings);
                    if (verification.IsValid)
                    {
                        break;
                    }

                    var action = await progressWindow.ShowErrorAsync(
                        LocalizationService.Format(activeSettings, "Update.VerificationFailed", verification.Error ?? string.Empty));
                    if (action != UpdateWindowAction.Retry)
                    {
                        return false;
                    }
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
                catch (Exception ex)
                {
                    var action = await progressWindow.ShowErrorAsync(
                        LocalizationService.Format(activeSettings, "Update.DownloadFailed", ex.Message));
                    if (action != UpdateWindowAction.Retry)
                    {
                        return false;
                    }
                }
            }

            return await progressWindow.ShowReadyAsync(release, verification) == UpdateWindowAction.Install &&
                InstallReadyUpdate();
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Show(progressWindow, activeSettings, "Update.DownloadFailed", MessageBoxImage.Error, ex.Message);
            return false;
        }
        finally
        {
            progressWindow.Close();
        }
    }

    public async Task<UpdateVerificationResult> DownloadAndVerifyAsync(
        UpdateRelease release,
        IProgress<UpdateProgressState> progress,
        CancellationToken cancellationToken,
        AppSettings? settings = null)
    {
        var activeSettings = settings ?? new AppSettings();
        var state = PrepareState(release);
        var partialPath = state.PartialPath!;
        Directory.CreateDirectory(Path.GetDirectoryName(partialPath)!);
        var existingLength = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;

        if (existingLength == release.Installer.Size && existingLength > 0)
        {
            progress.Report(new UpdateProgressState(LocalizationService.Text(activeSettings, "Update.Verifying")));
            return await VerifyDownloadedInstallerAsync(
                release,
                state,
                partialPath,
                existingLength,
                cancellationToken);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, release.Installer.DownloadUri);
        if (existingLength > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existingLength, null);
            if (EntityTagHeaderValue.TryParse(state.ETag, out var entityTag))
            {
                request.Headers.IfRange = new RangeConditionHeaderValue(entityTag);
            }
            else if (state.LastModified is not null)
            {
                request.Headers.IfRange = new RangeConditionHeaderValue(state.LastModified.Value);
            }
        }

        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && existingLength > 0)
        {
            File.Delete(partialPath);
            state.DownloadedBytes = 0;
            state.ETag = null;
            state.LastModified = null;
            stateService.Save(state);
            return await DownloadAndVerifyAsync(release, progress, cancellationToken, activeSettings);
        }

        response.EnsureSuccessStatusCode();
        var resumed = existingLength > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (resumed is false)
        {
            existingLength = 0;
        }

        state.ETag = response.Headers.ETag?.ToString();
        state.LastModified = response.Content.Headers.LastModified;
        stateService.Save(state);
        var totalBytes = response.Content.Headers.ContentRange?.Length ??
            (response.Content.Headers.ContentLength is long length ? length + existingLength : release.Installer.Size);
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(
            partialPath,
            resumed ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            true);
        var buffer = new byte[81920];
        var downloadedBytes = existingLength;
        var lastPersisted = downloadedBytes;

        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            downloadedBytes += bytesRead;
            state.DownloadedBytes = downloadedBytes;
            if (downloadedBytes - lastPersisted >= 1024 * 1024)
            {
                stateService.Save(state);
                lastPersisted = downloadedBytes;
            }

            progress.Report(new UpdateProgressState(
                LocalizationService.Format(activeSettings, "Update.Downloading", release.Tag),
                UpdateProgressCalculator.CalculatePercentage(downloadedBytes, totalBytes),
                downloadedBytes,
                totalBytes));
        }

        await destination.FlushAsync(cancellationToken);
        await destination.DisposeAsync();
        stateService.Save(state);
        progress.Report(new UpdateProgressState(LocalizationService.Text(activeSettings, "Update.Verifying")));
        return await VerifyDownloadedInstallerAsync(
            release,
            state,
            partialPath,
            downloadedBytes,
            cancellationToken);
    }

    private async Task<UpdateVerificationResult> VerifyDownloadedInstallerAsync(
        UpdateRelease release,
        UpdateDownloadState state,
        string partialPath,
        long downloadedBytes,
        CancellationToken cancellationToken)
    {
        var checksumText = await HttpClient.GetStringAsync(release.Checksum.DownloadUri, cancellationToken);
        var checksumDigest = UpdateHashPolicy.Normalize(checksumText);
        var calculatedDigest = await CalculateSha256Async(partialPath, cancellationToken);
        var hashResult = UpdateHashPolicy.Verify(release.Installer.Digest, checksumDigest, calculatedDigest);
        if (hashResult.IsValid is false)
        {
            DeleteInvalidFiles(state);
            return new(false, UpdateHashPolicy.Normalize(release.Installer.Digest), checksumDigest, calculatedDigest,
                UpdateSignatureStatus.Invalid, null, hashResult.Error);
        }

        var signature = AuthenticodeVerifier.Verify(partialPath);
        if (signature.Status == UpdateSignatureStatus.Invalid)
        {
            DeleteInvalidFiles(state);
            return new(false, hashResult.NormalizedDigest, checksumDigest, calculatedDigest,
                signature.Status, signature.Signer, signature.Error);
        }

        var readyPath = Path.ChangeExtension(partialPath, ".exe");
        File.Move(partialPath, readyPath, true);
        var verification = new UpdateVerificationResult(true, hashResult.NormalizedDigest, checksumDigest, calculatedDigest,
            signature.Status, signature.Signer, null);
        state.PartialPath = null;
        state.ReadyInstallerPath = readyPath;
        state.DownloadedBytes = downloadedBytes;
        state.Verification = verification;
        stateService.Save(state);
        return verification;
    }

    public bool InstallReadyUpdate()
    {
        var state = stateService.Load();
        if (state.Verification?.IsValid != true ||
            string.IsNullOrWhiteSpace(state.ReadyInstallerPath) ||
            File.Exists(state.ReadyInstallerPath) is false)
        {
            return false;
        }

        var calculatedDigest = CalculateSha256(state.ReadyInstallerPath);
        var hashResult = UpdateHashPolicy.Verify(
            state.Verification.GitHubDigest,
            state.Verification.ChecksumDigest,
            calculatedDigest);
        var signature = AuthenticodeVerifier.Verify(state.ReadyInstallerPath);
        if (hashResult.IsValid is false || signature.Status == UpdateSignatureStatus.Invalid)
        {
            DeleteInvalidFiles(state);
            return false;
        }

        try
        {
            state.InstallPending = true;
            stateService.Save(state);
            Process.Start(new ProcessStartInfo
            {
                FileName = state.ReadyInstallerPath,
                Arguments = "/SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
                UseShellExecute = true
            });
        }
        catch
        {
            state.InstallPending = false;
            stateService.Save(state);
            return false;
        }

        System.Windows.Application.Current.Shutdown();
        return true;
    }

    private UpdateDownloadState PrepareState(UpdateRelease release)
    {
        var state = stateService.Load();
        var partialPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Thinksnap",
            "Updates",
            release.Tag.TrimStart('v', 'V'),
            release.Installer.Name + ".part");
        if (string.Equals(state.Version, release.Tag, StringComparison.OrdinalIgnoreCase) is false)
        {
            state = new UpdateDownloadState { Version = release.Tag, PartialPath = partialPath };
        }
        else
        {
            state.PartialPath ??= partialPath;
        }

        stateService.Save(state);
        return state;
    }

    private void DeleteInvalidFiles(UpdateDownloadState state)
    {
        foreach (var path in new[] { state.PartialPath, state.ReadyInstallerPath })
        {
            if (string.IsNullOrWhiteSpace(path) is false && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        stateService.Clear();
    }

    private static async Task<IReadOnlyList<GitHubRelease>> ReadReleasesAsync(CancellationToken cancellationToken)
    {
        await using var stream = await HttpClient.GetStreamAsync(
            "https://api.github.com/repos/arlkn/thinksnap/releases?per_page=30",
            cancellationToken);
        return await JsonSerializer.DeserializeAsync<IReadOnlyList<GitHubRelease>>(stream, JsonOptions, cancellationToken) ??
            Array.Empty<GitHubRelease>();
    }

    private static UpdateAsset ToAsset(GitHubAsset asset, Uri uri) => new(asset.Name, uri, asset.Size, asset.Digest);

    private static GitHubAsset? SelectAsset(IReadOnlyList<GitHubAsset> assets, string suffix, string preferredName) =>
        assets.Where(asset => asset.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(asset => asset.Name.Contains(preferredName, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

    private static SemanticVersion GetCurrentVersion()
    {
        var assembly = typeof(UpdateService).Assembly;
        var value = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            assembly.GetName().Version?.ToString() ?? "0.0.0";
        return SemanticVersion.TryParse(value, out var version) ? version : new(0, 0, 0, Array.Empty<string>());
    }

    private static async Task<string> CalculateSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var algorithm = SHA256.Create();
        return Convert.ToHexString(await algorithm.ComputeHashAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static string CalculateSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var algorithm = SHA256.Create();
        return Convert.ToHexString(algorithm.ComputeHash(stream)).ToLowerInvariant();
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Thinksnap-Updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static string FormatBytes(long bytes) => bytes <= 0 ? "Unknown size" : $"{bytes / 1024d / 1024d:0.0} MB";

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
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("assets")] IReadOnlyList<GitHubAsset> Assets);

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("digest")] string? Digest);
}
