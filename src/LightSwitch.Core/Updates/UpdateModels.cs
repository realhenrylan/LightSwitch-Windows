namespace LightSwitch.Core.Updates;

/// <summary>How the running executable was installed.</summary>
public enum InstallationMode
{
    /// <summary>Installed by the Inno Setup package (an uninstaller lives next to the exe).</summary>
    Installed,

    /// <summary>Extracted from the portable ZIP; files must never be replaced while running.</summary>
    Portable,
}

public enum ReleaseFetchStatus
{
    Success,
    NetworkError,
    Timeout,
    RateLimited,
    HttpError,
    InvalidResponse,
    Canceled,
}

public enum UpdateCheckStatus
{
    UpdateAvailable,
    UpToDate,
    NoStableRelease,
    Throttled,
    NetworkError,
    Timeout,
    RateLimited,
    HttpError,
    InvalidResponse,
    Canceled,
}

public enum UpdateDownloadStatus
{
    Succeeded,
    MissingAsset,
    MissingChecksum,
    InvalidAsset,
    ChecksumMismatch,
    TooLarge,
    HttpError,
    NetworkError,
    Timeout,
    RateLimited,
    InvalidResponse,
    IoError,
    Canceled,
}

public sealed record ReleaseAsset(string Name, string DownloadUrl, long Size);

public sealed record ReleaseInfo(
    string TagName,
    SemanticVersion Version,
    string Title,
    string Notes,
    string HtmlUrl,
    DateTimeOffset? PublishedAt,
    bool IsDraft,
    bool IsPrerelease,
    IReadOnlyList<ReleaseAsset> Assets)
{
    public ReleaseAsset? FindAsset(string name) =>
        Assets.FirstOrDefault(asset => string.Equals(asset.Name, name, StringComparison.OrdinalIgnoreCase));
}

public sealed record ReleaseFetchResult(
    ReleaseFetchStatus Status,
    IReadOnlyList<ReleaseInfo> Releases,
    string Message)
{
    public bool Success => Status == ReleaseFetchStatus.Success;

    public static ReleaseFetchResult Ok(IReadOnlyList<ReleaseInfo> releases) =>
        new(ReleaseFetchStatus.Success, releases, string.Empty);

    public static ReleaseFetchResult Fail(ReleaseFetchStatus status, string message) =>
        new(status, Array.Empty<ReleaseInfo>(), message);
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    ReleaseInfo? Release,
    string Message,
    DateTimeOffset CheckedAt)
{
    public bool HasUpdate => Status == UpdateCheckStatus.UpdateAvailable && Release is not null;

    public static UpdateCheckResult Available(ReleaseInfo release, DateTimeOffset checkedAt) =>
        new(UpdateCheckStatus.UpdateAvailable, release,
            $"发现新版本 {release.Version}。", checkedAt);

    public static UpdateCheckResult UpToDate(SemanticVersion current, DateTimeOffset checkedAt) =>
        new(UpdateCheckStatus.UpToDate, null, $"已是最新版本（v{current}）。", checkedAt);

    public static UpdateCheckResult NoStable(SemanticVersion current, DateTimeOffset checkedAt) =>
        new(UpdateCheckStatus.NoStableRelease, null,
            $"尚未找到可用的正式版本（当前 v{current}）。", checkedAt);

    public static UpdateCheckResult Throttled(DateTimeOffset checkedAt) =>
        new(UpdateCheckStatus.Throttled, null, "距上次检查不足 24 小时，已跳过本次自动检查。", checkedAt);

    public static UpdateCheckResult Failure(UpdateCheckStatus status, string message, DateTimeOffset checkedAt) =>
        new(status, null, message, checkedAt);
}

public sealed record UpdateDownloadResult(
    UpdateDownloadStatus Status,
    string? FilePath,
    string Message,
    string? ExpectedHash = null,
    string? ActualHash = null)
{
    public bool Success => Status == UpdateDownloadStatus.Succeeded && FilePath is not null;

    public static UpdateDownloadResult Fail(UpdateDownloadStatus status, string message,
        string? expectedHash = null, string? actualHash = null) =>
        new(status, null, message, expectedHash, actualHash);
}

/// <summary>Immutable knobs shared by the release client and the downloader.</summary>
public sealed class UpdateCheckOptions
{
    public Uri ReleasesApi { get; init; } =
        new("https://api.github.com/repos/realhenrylan/LightSwitch-Windows/releases?per_page=100");

    public string UserAgent { get; init; } = "LightSwitch";

    public TimeSpan CheckTimeout { get; init; } = TimeSpan.FromSeconds(20);

    public TimeSpan DownloadTimeout { get; init; } = TimeSpan.FromMinutes(10);

    public long MaxDownloadBytes { get; init; } = 512L * 1024 * 1024;
}
