using System.Net;
using System.Security.Cryptography;
using LightSwitch.Core.Updates;
using Xunit;

namespace LightSwitch.Tests;

public sealed class SemanticVersionTests
{
    [Theory]
    [InlineData("2.0.3", "2.0.10", -1)]
    [InlineData("2.0.10", "2.0.3", 1)]
    [InlineData("v2.1.0", "2.1.0", 0)]
    [InlineData("2.1.0+build.5", "2.1.0", 0)]
    [InlineData("2.1", "2.1.0", 0)]
    [InlineData("2.1.0", "2.1.1", -1)]
    [InlineData("2.1.0", "2.1.0-beta.1", 1)]
    [InlineData("2.1.0-beta.1", "2.1.0", -1)]
    [InlineData("2.1.0-beta.2", "2.1.0-beta.10", -1)]
    [InlineData("2.1.0-beta", "2.1.0-beta.1", -1)]
    [InlineData("2.1.0-alpha", "2.1.0-beta", -1)]
    [InlineData("2.1.0-rc.1", "2.1.0-rc.1", 0)]
    public void VersionsCompareSemantically(string left, string right, int expected)
    {
        var comparison = SemanticVersion.Parse(left).CompareTo(SemanticVersion.Parse(right));
        Assert.Equal(expected, Math.Sign(comparison));
    }

    [Theory]
    [InlineData("")]
    [InlineData("v")]
    [InlineData("abc")]
    [InlineData("1.2.3.4")]
    [InlineData("1..2")]
    [InlineData("1.2.-3")]
    [InlineData("1.2.3-")]
    [InlineData("-1.2.3")]
    public void InvalidTextIsRejected(string text)
    {
        Assert.False(SemanticVersion.TryParse(text, out _));
    }

    [Fact]
    public void PreReleaseNumbersAreNormalized()
    {
        var version = SemanticVersion.Parse("v2.1.0-beta.01");
        Assert.True(version.IsPreRelease);
        Assert.Equal("2.1.0-beta.1", version.ToString());
        Assert.True(version == SemanticVersion.Parse("2.1.0-beta.1"));
    }
}

public sealed class ReleaseSelectionTests
{
    [Fact]
    public void DraftsAndPreReleasesAreIgnored()
    {
        var releases = new[]
        {
            Release("v2.5.0", draft: true),
            Release("v2.6.0", prerelease: true),
            Release("v2.7.0-beta.1"),
            Release("v2.3.0"),
            Release("v2.4.0"),
        };

        var latest = ReleaseSelector.SelectLatestStable(releases);
        Assert.NotNull(latest);
        Assert.Equal("2.4.0", latest!.Version.ToString());
    }

    [Fact]
    public void OnlyPreReleasesMeansNoStableRelease()
    {
        var releases = new[]
        {
            Release("v3.0.0-beta.1"),
            Release("v3.0.0-rc.1", prerelease: true),
            Release("v3.0.0", draft: true),
        };

        Assert.Null(ReleaseSelector.SelectLatestStable(releases));
    }

    [Fact]
    public void AutoCheckRespectsTheTwentyFourHourInterval()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.True(UpdatePolicy.ShouldAutoCheck(true, null, now));
        Assert.False(UpdatePolicy.ShouldAutoCheck(true, now.AddHours(-1), now));
        Assert.True(UpdatePolicy.ShouldAutoCheck(true, now.AddHours(-25), now));
        Assert.False(UpdatePolicy.ShouldAutoCheck(false, null, now));
        Assert.True(UpdatePolicy.ShouldAutoCheck(true, now.AddHours(1), now));
    }

    [Fact]
    public void InstallationModeUsesTheUninstallerMarker()
    {
        var exe = Path.Combine(Path.GetTempPath(), "LightSwitch", "LightSwitch.exe");

        Assert.Equal(InstallationMode.Installed,
            InstallationModeDetector.Detect(exe, path => path.EndsWith("unins000.exe", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(InstallationMode.Portable, InstallationModeDetector.Detect(exe, _ => false));
        Assert.Equal(InstallationMode.Portable, InstallationModeDetector.Detect(null));
    }

    [Fact]
    public void AssetSelectionSplitsInstalledAndPortable()
    {
        var setup = Asset("LightSwitch-2.2.0-setup.exe");
        var portable = Asset("LightSwitch-2.2.0-portable.zip");
        var release = Release("v2.2.0", assets: new[] { setup, portable });

        Assert.Equal("LightSwitch-2.2.0-setup.exe",
            ReleaseAssetSelector.Select(release, InstallationMode.Installed)!.Name);
        Assert.Equal("LightSwitch-2.2.0-portable.zip",
            ReleaseAssetSelector.Select(release, InstallationMode.Portable)!.Name);
        Assert.Equal("LightSwitch-2.2.0-setup.exe",
            ReleaseAssetSelector.ExpectedAssetName(release.Version, InstallationMode.Installed));
        Assert.Equal("LightSwitch-2.2.0-portable.zip",
            ReleaseAssetSelector.ExpectedAssetName(release.Version, InstallationMode.Portable));
    }

    [Fact]
    public void AssetForTheOtherModeIsNotAccepted()
    {
        var release = Release("v2.2.0", assets: new[] { Asset("LightSwitch-2.2.0-setup.exe") });
        Assert.Null(ReleaseAssetSelector.Select(release, InstallationMode.Portable));
    }

    [Fact]
    public void AssetFromAnotherVersionIsNotAccepted()
    {
        var release = Release("v2.2.0", assets: new[] { Asset("LightSwitch-2.1.0-setup.exe") });
        Assert.Null(ReleaseAssetSelector.Select(release, InstallationMode.Installed));
    }

    private static ReleaseInfo Release(string tag, bool draft = false, bool prerelease = false,
        params ReleaseAsset[] assets) =>
        new(tag, SemanticVersion.Parse(tag), tag, $"notes for {tag}",
            "https://github.com/realhenrylan/LightSwitch-Windows/releases",
            null, draft, prerelease, assets);

    private static ReleaseAsset Asset(string name) =>
        new(name,
            $"https://github.com/realhenrylan/LightSwitch-Windows/releases/download/v2.2.0/{name}",
            100);
}

public sealed class UpdateServiceTests
{
    [Fact]
    public void AutomaticCheckIsThrottledAfterARecentCheck()
    {
        var now = DateTimeOffset.UtcNow;
        var statePath = TempStatePath();
        try
        {
            new UpdateStateStore(statePath).Save(new UpdateState { LastCheckUtc = now.AddHours(-1) });
            var source = new FakeReleaseSource { Result = ReleaseFetchResult.Ok(new[] { Release("v2.2.0") }) };
            var service = CreateService(SemanticVersion.Parse("2.1.0"), source, statePath, now);

            var result = service.CheckAsync(manual: false, CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(UpdateCheckStatus.Throttled, result.Status);
            Assert.Equal(0, source.Calls);
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [Fact]
    public void ManualCheckBypassesTheThrottleAndFindsAnUpdate()
    {
        var now = DateTimeOffset.UtcNow;
        var statePath = TempStatePath();
        try
        {
            new UpdateStateStore(statePath).Save(new UpdateState { LastCheckUtc = now.AddMinutes(-5) });
            var source = new FakeReleaseSource { Result = ReleaseFetchResult.Ok(new[] { Release("v2.2.0") }) };
            var service = CreateService(SemanticVersion.Parse("2.1.0"), source, statePath, now);

            var result = service.CheckAsync(manual: true, CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
            Assert.True(result.HasUpdate);
            Assert.Equal("2.2.0", result.Release!.Version.ToString());
            Assert.Equal(1, source.Calls);
            Assert.Equal(now, new UpdateStateStore(statePath).Load().LastCheckUtc);
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [Fact]
    public void CurrentVersionIsReportedAsUpToDate()
    {
        var statePath = TempStatePath();
        try
        {
            var source = new FakeReleaseSource { Result = ReleaseFetchResult.Ok(new[] { Release("v2.1.0") }) };
            var service = CreateService(SemanticVersion.Parse("2.1.0"), source, statePath, DateTimeOffset.UtcNow);

            var result = service.CheckAsync(manual: true, CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
            Assert.False(result.HasUpdate);
            Assert.Null(result.Release);
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [Fact]
    public void PreReleasesDoNotCountAsAnUpdate()
    {
        var statePath = TempStatePath();
        try
        {
            var source = new FakeReleaseSource
            {
                Result = ReleaseFetchResult.Ok(new[] { Release("v2.5.0-beta.1") }),
            };
            var service = CreateService(SemanticVersion.Parse("2.1.0"), source, statePath, DateTimeOffset.UtcNow);

            var result = service.CheckAsync(manual: true, CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(UpdateCheckStatus.NoStableRelease, result.Status);
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [Fact]
    public void FailedAttemptIsRecordedSoTheNextAutomaticCheckIsThrottled()
    {
        var now = DateTimeOffset.UtcNow;
        var statePath = TempStatePath();
        try
        {
            var source = new FakeReleaseSource
            {
                Result = ReleaseFetchResult.Fail(ReleaseFetchStatus.NetworkError, "offline"),
            };
            var service = CreateService(SemanticVersion.Parse("2.1.0"), source, statePath, now);

            var first = service.CheckAsync(manual: false, CancellationToken.None).GetAwaiter().GetResult();
            var second = service.CheckAsync(manual: false, CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(UpdateCheckStatus.NetworkError, first.Status);
            Assert.Equal(UpdateCheckStatus.Throttled, second.Status);
            Assert.Equal(1, source.Calls);
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [Fact]
    public void TimeoutAndRateLimitStatusesAreMapped()
    {
        var statePath = TempStatePath();
        try
        {
            var source = new FakeReleaseSource
            {
                Result = ReleaseFetchResult.Fail(ReleaseFetchStatus.Timeout, "timed out"),
            };
            var service = CreateService(SemanticVersion.Parse("2.1.0"), source, statePath, DateTimeOffset.UtcNow);
            var timeout = service.CheckAsync(manual: true, CancellationToken.None).GetAwaiter().GetResult();
            Assert.Equal(UpdateCheckStatus.Timeout, timeout.Status);

            source.Result = ReleaseFetchResult.Fail(ReleaseFetchStatus.RateLimited, "rate limited");
            var limited = service.CheckAsync(manual: true, CancellationToken.None).GetAwaiter().GetResult();
            Assert.Equal(UpdateCheckStatus.RateLimited, limited.Status);
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [Fact]
    public void PostponedVersionIsPersistedAndRecognized()
    {
        var statePath = TempStatePath();
        try
        {
            var service = CreateService(SemanticVersion.Parse("2.1.0"),
                new FakeReleaseSource(), statePath, DateTimeOffset.UtcNow);

            service.Postpone(SemanticVersion.Parse("2.2.0"));

            Assert.True(service.IsPostponed(SemanticVersion.Parse("2.2.0")));
            Assert.False(service.IsPostponed(SemanticVersion.Parse("2.3.0")));
            Assert.Equal("2.2.0", new UpdateStateStore(statePath).Load().PostponedVersion);
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [Fact]
    public void MissingAssetIsReportedBeforeDownloading()
    {
        var statePath = TempStatePath();
        try
        {
            var downloader = new FakeDownloader();
            var service = CreateService(SemanticVersion.Parse("2.1.0"), new FakeReleaseSource(), statePath,
                DateTimeOffset.UtcNow, downloader);
            var release = Release("v2.2.0", assets: new[] { Asset("LightSwitch-2.2.0-portable.zip") });

            var result = service.DownloadAsync(release, null, CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(UpdateDownloadStatus.MissingAsset, result.Status);
            Assert.True(result.Message.Contains("setup.exe"));
            Assert.Equal(0, downloader.Calls);
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [Fact]
    public void MissingChecksumIsReportedBeforeDownloading()
    {
        var statePath = TempStatePath();
        try
        {
            var downloader = new FakeDownloader();
            var service = CreateService(SemanticVersion.Parse("2.1.0"), new FakeReleaseSource(), statePath,
                DateTimeOffset.UtcNow, downloader);
            var release = Release("v2.2.0", assets: new[] { Asset("LightSwitch-2.2.0-setup.exe") });

            var result = service.DownloadAsync(release, null, CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(UpdateDownloadStatus.MissingChecksum, result.Status);
            Assert.Equal(0, downloader.Calls);
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    [Fact]
    public void DownloadUsesTheAssetForTheDetectedInstallationMode()
    {
        var statePath = TempStatePath();
        try
        {
            var downloader = new FakeDownloader();
            var service = CreateService(SemanticVersion.Parse("2.1.0"), new FakeReleaseSource(), statePath,
                DateTimeOffset.UtcNow, downloader);
            var setup = Asset("LightSwitch-2.2.0-setup.exe");
            var checksum = Asset("LightSwitch-2.2.0-setup.exe.sha256");
            var release = Release("v2.2.0", assets: new[] { setup, checksum, Asset("LightSwitch-2.2.0-portable.zip") });

            var result = service.DownloadAsync(release, null, CancellationToken.None).GetAwaiter().GetResult();

            Assert.True(result.Success);
            Assert.Equal("LightSwitch-2.2.0-setup.exe", downloader.LastAsset!.Name);
            Assert.Equal("LightSwitch-2.2.0-setup.exe.sha256", downloader.LastChecksum!.Name);
        }
        finally
        {
            File.Delete(statePath);
        }
    }

    private static UpdateService CreateService(SemanticVersion current, IReleaseSource source, string statePath,
        DateTimeOffset now, IUpdateFileDownloader? downloader = null) =>
        new(current, InstallationMode.Installed, new UpdateStateStore(statePath), source,
            downloader ?? new FakeDownloader(),
            Path.Combine(Path.GetTempPath(), $"LightSwitch-downloads-{Guid.NewGuid():N}"),
            new FakeTimeProvider(now));

    private static string TempStatePath() =>
        Path.Combine(Path.GetTempPath(), $"LightSwitch-update-state-{Guid.NewGuid():N}.json");

    private static ReleaseInfo Release(string tag, params ReleaseAsset[] assets) =>
        new(tag, SemanticVersion.Parse(tag), tag, $"notes for {tag}",
            "https://github.com/realhenrylan/LightSwitch-Windows/releases",
            null, false, false, assets);

    private static ReleaseAsset Asset(string name) =>
        new(name,
            $"https://github.com/realhenrylan/LightSwitch-Windows/releases/download/v2.2.0/{name}",
            100);

    private sealed class FakeTimeProvider : TimeProvider
    {
        public FakeTimeProvider(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; }

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class FakeReleaseSource : IReleaseSource
    {
        public ReleaseFetchResult Result { get; set; } =
            ReleaseFetchResult.Ok(Array.Empty<ReleaseInfo>());

        public int Calls { get; private set; }

        public Task<ReleaseFetchResult> FetchReleasesAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeDownloader : IUpdateFileDownloader
    {
        public UpdateDownloadResult Result { get; set; } = new(
            UpdateDownloadStatus.Succeeded,
            Path.Combine(Path.GetTempPath(), "LightSwitch-fake-update.exe"),
            "downloaded");

        public int Calls { get; private set; }

        public ReleaseAsset? LastAsset { get; private set; }

        public ReleaseAsset? LastChecksum { get; private set; }

        public Task<UpdateDownloadResult> DownloadAsync(ReleaseAsset asset, ReleaseAsset checksumAsset,
            string destinationDirectory, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            Calls++;
            LastAsset = asset;
            LastChecksum = checksumAsset;
            return Task.FromResult(Result);
        }
    }
}

public sealed class GitHubReleaseClientTests
{
    [Fact]
    public void ReleasesAreParsedAndInvalidTagsAreSkipped()
    {
        const string json = """
            [
              {
                "tag_name": "v2.2.0",
                "name": "LightSwitch 2.2.0",
                "body": "## 更新内容\n- 修复问题",
                "html_url": "https://github.com/realhenrylan/LightSwitch-Windows/releases/tag/v2.2.0",
                "published_at": "2026-01-02T03:04:05Z",
                "draft": false,
                "prerelease": false,
                "assets": [
                  {
                    "name": "LightSwitch-2.2.0-setup.exe",
                    "browser_download_url": "https://github.com/realhenrylan/LightSwitch-Windows/releases/download/v2.2.0/LightSwitch-2.2.0-setup.exe",
                    "size": 123,
                    "content_type": "application/octet-stream"
                  }
                ]
              },
              { "tag_name": "nightly", "name": "no version" }
            ]
            """;

        using var client = new GitHubReleaseClient(new UpdateCheckOptions(), new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json),
            })));

        var result = client.FetchReleasesAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.True(result.Success);
        Assert.Equal(1, result.Releases.Count);
        var release = result.Releases[0];
        Assert.Equal("2.2.0", release.Version.ToString());
        Assert.Equal("LightSwitch 2.2.0", release.Title);
        Assert.True(release.Notes.Contains("修复问题"));
        Assert.NotNull(release.PublishedAt);
        Assert.Equal("LightSwitch-2.2.0-setup.exe", release.Assets[0].Name);
        Assert.Equal(123, release.Assets[0].Size);
    }

    [Fact]
    public void RateLimitHeadersBecomeANonBlockingResult()
    {
        using var client = new GitHubReleaseClient(new UpdateCheckOptions(), new StubHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
            response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "0");
            return Task.FromResult(response);
        }));

        var result = client.FetchReleasesAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(ReleaseFetchStatus.RateLimited, result.Status);
        Assert.False(result.Success);
    }

    [Fact]
    public void TimeoutIsReportedAsATimeout()
    {
        var options = new UpdateCheckOptions { CheckTimeout = TimeSpan.FromMilliseconds(60) };
        using var client = new GitHubReleaseClient(options, new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        var result = client.FetchReleasesAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(ReleaseFetchStatus.Timeout, result.Status);
    }

    [Fact]
    public void OfflineIsReportedAsANetworkError()
    {
        using var client = new GitHubReleaseClient(new UpdateCheckOptions(), new StubHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"))));

        var result = client.FetchReleasesAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(ReleaseFetchStatus.NetworkError, result.Status);
    }

    [Fact]
    public void MalformedJsonIsReportedAsInvalidResponse()
    {
        using var client = new GitHubReleaseClient(new UpdateCheckOptions(), new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"not\":\"an array\"}"),
            })));

        var result = client.FetchReleasesAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(ReleaseFetchStatus.InvalidResponse, result.Status);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => _handler(request, cancellationToken);
    }
}

public sealed class UpdateDownloaderTests
{
    private const string FileName = "LightSwitch-2.1.0-setup.exe";
    private const string SetupUrl =
        "https://github.com/realhenrylan/LightSwitch-Windows/releases/download/v2.1.0/LightSwitch-2.1.0-setup.exe";
    private const string ChecksumUrl = SetupUrl + ".sha256";

    [Fact]
    public void VerifiedDownloadIsWrittenAndReturned()
    {
        var directory = TempDirectory();
        try
        {
            var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var hash = Hash(payload);
            using var downloader = new UpdateDownloader(new UpdateCheckOptions(),
                DownloadHandler(hash, payload));

            var result = downloader.DownloadAsync(Asset(), ChecksumAsset(hash), directory, null,
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.True(result.Success);
            Assert.Equal(hash, result.ActualHash);
            Assert.True(File.Exists(result.FilePath));
            Assert.Equal(Convert.ToHexString(payload),
                Convert.ToHexString(File.ReadAllBytes(result.FilePath!)));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ChecksumMismatchDeletesTheDownloadedFile()
    {
        var directory = TempDirectory();
        try
        {
            var payload = new byte[] { 1, 2, 3, 4 };
            var wrongHash = Hash(new byte[] { 9, 9, 9, 9 });
            using var downloader = new UpdateDownloader(new UpdateCheckOptions(),
                DownloadHandler(wrongHash, payload));

            var result = downloader.DownloadAsync(Asset(), ChecksumAsset(wrongHash), directory, null,
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(UpdateDownloadStatus.ChecksumMismatch, result.Status);
            Assert.Null(result.FilePath);
            Assert.False(File.Exists(Path.Combine(directory, FileName)));
            Assert.False(File.Exists(Path.Combine(directory, FileName + ".part")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void NonGitHubUrlsAreRejectedWithoutNetworkAccess()
    {
        var directory = TempDirectory();
        try
        {
            var requests = 0;
            var handler = new StubHandler((_, _) =>
            {
                requests++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });
            using var downloader = new UpdateDownloader(new UpdateCheckOptions(), handler);
            var asset = new ReleaseAsset(FileName, "https://example.com/" + FileName, 10);

            var result = downloader.DownloadAsync(asset, ChecksumAsset("0".PadLeft(64, '0')), directory, null,
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(UpdateDownloadStatus.InvalidAsset, result.Status);
            Assert.Equal(0, requests);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void OversizedPackageIsRejected()
    {
        var directory = TempDirectory();
        try
        {
            var payload = new byte[] { 1, 2, 3, 4 };
            var hash = Hash(payload);
            using var downloader = new UpdateDownloader(
                new UpdateCheckOptions { MaxDownloadBytes = 2 }, DownloadHandler(hash, payload));

            var result = downloader.DownloadAsync(Asset(), ChecksumAsset(hash), directory, null,
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(UpdateDownloadStatus.TooLarge, result.Status);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string Hash(byte[] payload) =>
        Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

    private static ReleaseAsset Asset() => new(FileName, SetupUrl, 8);

    private static ReleaseAsset ChecksumAsset(string hash) =>
        new(FileName + ".sha256", ChecksumUrl, hash.Length + FileName.Length + 2);

    private static StubHandler DownloadHandler(string hash, byte[] payload) =>
        new((request, _) =>
        {
            if (request.RequestUri == new Uri(ChecksumUrl))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{hash}  {FileName}"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            });
        });

    private static string TempDirectory() =>
        Path.Combine(Path.GetTempPath(), $"LightSwitch-update-test-{Guid.NewGuid():N}");

    private static void DeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup for a temporary directory.
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => _handler(request, cancellationToken);
    }
}
