namespace LightSwitch.Core.Updates;

/// <summary>
/// UI-independent update workflow: fetch releases, compare semantic versions,
/// choose the asset for this installation, download and verify it.
/// </summary>
public sealed class UpdateService
{
    private readonly object _gate = new();
    private readonly UpdateStateStore _stateStore;
    private readonly IReleaseSource _releaseSource;
    private readonly IUpdateFileDownloader _downloader;
    private readonly TimeProvider _timeProvider;
    private readonly string _downloadDirectory;
    private UpdateState? _state;

    public UpdateService(
        SemanticVersion currentVersion,
        InstallationMode installationMode,
        UpdateStateStore stateStore,
        IReleaseSource releaseSource,
        IUpdateFileDownloader downloader,
        string downloadDirectory,
        TimeProvider? timeProvider = null)
    {
        CurrentVersion = currentVersion;
        InstallationMode = installationMode;
        _stateStore = stateStore;
        _releaseSource = releaseSource;
        _downloader = downloader;
        _downloadDirectory = downloadDirectory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public SemanticVersion CurrentVersion { get; }

    public InstallationMode InstallationMode { get; }

    public async Task<UpdateCheckResult> CheckAsync(bool manual, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        if (!manual && !UpdatePolicy.ShouldAutoCheck(true, GetState().LastCheckUtc, now))
        {
            return UpdateCheckResult.Throttled(now);
        }

        var fetch = await _releaseSource.FetchReleasesAsync(cancellationToken).ConfigureAwait(false);
        var completedAt = _timeProvider.GetUtcNow();

        if (fetch.Status == ReleaseFetchStatus.Canceled)
        {
            return UpdateCheckResult.Failure(UpdateCheckStatus.Canceled, fetch.Message, completedAt);
        }

        // Record failed attempts too: the 24h throttle is about request volume,
        // not about only remembering successful checks. Manual checks bypass it.
        RecordAttempt(completedAt);

        if (!fetch.Success)
        {
            return UpdateCheckResult.Failure(MapStatus(fetch.Status), fetch.Message, completedAt);
        }

        var latest = ReleaseSelector.SelectLatestStable(fetch.Releases);
        if (latest is null)
        {
            return UpdateCheckResult.NoStable(CurrentVersion, completedAt);
        }

        return latest.Version > CurrentVersion
            ? UpdateCheckResult.Available(latest, completedAt)
            : UpdateCheckResult.UpToDate(CurrentVersion, completedAt);
    }

    public bool IsPostponed(SemanticVersion version)
    {
        lock (_gate)
        {
            _state ??= _stateStore.Load();
            return SemanticVersion.TryParse(_state.PostponedVersion, out var postponed) &&
                   postponed == version;
        }
    }

    public void Postpone(SemanticVersion version)
    {
        lock (_gate)
        {
            _state ??= _stateStore.Load();
            _state.PostponedVersion = version.ToString();
            _stateStore.Save(_state);
        }
    }

    public async Task<UpdateDownloadResult> DownloadAsync(ReleaseInfo release, IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var asset = ReleaseAssetSelector.Select(release, InstallationMode);
        if (asset is null)
        {
            var expected = ReleaseAssetSelector.ExpectedAssetName(release.Version, InstallationMode);
            return UpdateDownloadResult.Fail(UpdateDownloadStatus.MissingAsset,
                $"该版本没有适用于{(InstallationMode == InstallationMode.Installed ? "安装版" : "便携版")}的更新包（{expected}）。");
        }

        var checksumAsset = release.FindAsset(asset.Name + ".sha256");
        if (checksumAsset is null)
        {
            return UpdateDownloadResult.Fail(UpdateDownloadStatus.MissingChecksum,
                "该版本缺少 SHA256 校验文件，出于安全考虑不会下载安装。");
        }

        var result = await _downloader
            .DownloadAsync(asset, checksumAsset, _downloadDirectory, progress, cancellationToken)
            .ConfigureAwait(false);
        if (result.Success)
        {
            Log.Info($"Update package verified: {result.FilePath}");
        }
        else if (result.Status != UpdateDownloadStatus.Canceled)
        {
            Log.Warn($"Update download failed ({result.Status}): {result.Message}");
        }

        return result;
    }

    private UpdateState GetState()
    {
        lock (_gate)
        {
            _state ??= _stateStore.Load();
            return _state.Clone();
        }
    }

    private void RecordAttempt(DateTimeOffset at)
    {
        lock (_gate)
        {
            _state ??= _stateStore.Load();
            _state.LastCheckUtc = at;
            _stateStore.Save(_state);
        }
    }

    private static UpdateCheckStatus MapStatus(ReleaseFetchStatus status) => status switch
    {
        ReleaseFetchStatus.NetworkError => UpdateCheckStatus.NetworkError,
        ReleaseFetchStatus.Timeout => UpdateCheckStatus.Timeout,
        ReleaseFetchStatus.RateLimited => UpdateCheckStatus.RateLimited,
        ReleaseFetchStatus.HttpError => UpdateCheckStatus.HttpError,
        ReleaseFetchStatus.InvalidResponse => UpdateCheckStatus.InvalidResponse,
        ReleaseFetchStatus.Canceled => UpdateCheckStatus.Canceled,
        _ => UpdateCheckStatus.InvalidResponse,
    };
}
