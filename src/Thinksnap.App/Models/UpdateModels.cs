using Thinksnap.Core.Updates;

namespace Thinksnap.App.Models;

public sealed record UpdateAsset(
    string Name,
    Uri DownloadUri,
    long Size,
    string? Digest,
    string? ETag = null,
    DateTimeOffset? LastModified = null);

public sealed record UpdateRelease(
    string Tag,
    SemanticVersion Version,
    UpdateChannel Channel,
    string Notes,
    Uri ReleasePage,
    UpdateAsset Installer,
    UpdateAsset Checksum);

public enum UpdateSignatureStatus
{
    Valid,
    Unsigned,
    Invalid
}

public sealed record UpdateVerificationResult(
    bool IsValid,
    string? GitHubDigest,
    string? ChecksumDigest,
    string? CalculatedDigest,
    UpdateSignatureStatus SignatureStatus,
    string? Signer,
    string? Error);

public sealed class UpdateDownloadState
{
    public string? Version { get; set; }
    public string? PartialPath { get; set; }
    public string? ReadyInstallerPath { get; set; }
    public long DownloadedBytes { get; set; }
    public string? ETag { get; set; }
    public DateTimeOffset? LastModified { get; set; }
    public UpdateVerificationResult? Verification { get; set; }
}

public enum UpdateWindowAction
{
    Download,
    Install,
    Later,
    Retry,
    Cancel
}
