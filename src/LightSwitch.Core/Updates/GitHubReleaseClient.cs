using System.Net;
using System.Text.Json;

namespace LightSwitch.Core.Updates;

public interface IReleaseSource
{
    Task<ReleaseFetchResult> FetchReleasesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Reads the official GitHub Releases API over HTTPS. The response is fully
/// validated before it reaches the UI; failures are returned as data instead of
/// exceptions so a background check can never take the app down.
/// </summary>
public sealed class GitHubReleaseClient : IReleaseSource, IDisposable
{
    private const string AcceptHeader = "application/vnd.github+json";
    private readonly HttpClient _httpClient;
    private readonly UpdateCheckOptions _options;

    public GitHubReleaseClient(UpdateCheckOptions? options = null, HttpMessageHandler? handler = null)
    {
        _options = options ?? new UpdateCheckOptions();
        _httpClient = new HttpClient(handler ?? new HttpClientHandler())
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd(AcceptHeader);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<ReleaseFetchResult> FetchReleasesAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.CheckTimeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _options.ReleasesApi);
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            if (IsRateLimited(response))
            {
                return ReleaseFetchResult.Fail(ReleaseFetchStatus.RateLimited, BuildRateLimitMessage(response));
            }

            if (!response.IsSuccessStatusCode)
            {
                return ReleaseFetchResult.Fail(ReleaseFetchStatus.HttpError,
                    $"GitHub Releases 接口返回 HTTP {(int)response.StatusCode}。");
            }

            var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            var releases = ParseReleases(json);
            return ReleaseFetchResult.Ok(releases);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ReleaseFetchResult.Fail(ReleaseFetchStatus.Canceled, "已取消检查更新。");
        }
        catch (OperationCanceledException)
        {
            return ReleaseFetchResult.Fail(ReleaseFetchStatus.Timeout,
                "连接 GitHub 超时，请检查网络后重试。");
        }
        catch (HttpRequestException ex)
        {
            Log.Warn($"Update check failed to reach GitHub: {ex.Message}");
            return ReleaseFetchResult.Fail(ReleaseFetchStatus.NetworkError,
                "无法连接 GitHub，请检查网络后重试。");
        }
        catch (JsonException ex)
        {
            Log.Warn($"Update check received invalid JSON: {ex.Message}");
            return ReleaseFetchResult.Fail(ReleaseFetchStatus.InvalidResponse,
                "GitHub 返回了无法解析的版本信息。");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException)
        {
            Log.Warn($"Update check failed: {ex.Message}");
            return ReleaseFetchResult.Fail(ReleaseFetchStatus.NetworkError,
                $"检查更新失败：{ex.Message}");
        }
    }

    public void Dispose() => _httpClient.Dispose();

    private static bool IsRateLimited(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return true;
        }

        if (response.StatusCode != HttpStatusCode.Forbidden)
        {
            return false;
        }

        return response.Headers.TryGetValues("X-RateLimit-Remaining", out var values) &&
               values.FirstOrDefault() == "0";
    }

    private static string BuildRateLimitMessage(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var values) &&
            long.TryParse(values.FirstOrDefault(), out var resetSeconds))
        {
            try
            {
                var local = DateTimeOffset.FromUnixTimeSeconds(resetSeconds).ToLocalTime();
                return $"GitHub API 请求频率受限，请在 {local:HH:mm} 后重试。";
            }
            catch (ArgumentOutOfRangeException)
            {
                // Fall through to the generic message.
            }
        }

        return "GitHub API 请求频率受限，请稍后重试。";
    }

    private static IReadOnlyList<ReleaseInfo> ParseReleases(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Expected a JSON array of releases.");
        }

        var releases = new List<ReleaseInfo>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var tag = GetString(element, "tag_name");
            if (!SemanticVersion.TryParse(tag, out var version))
            {
                continue;
            }

            var assets = new List<ReleaseAsset>();
            if (element.TryGetProperty("assets", out var assetsElement) &&
                assetsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var assetElement in assetsElement.EnumerateArray())
                {
                    if (assetElement.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var name = GetString(assetElement, "name");
                    var url = GetString(assetElement, "browser_download_url");
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                    {
                        continue;
                    }

                    assets.Add(new ReleaseAsset(name, url, GetInt64(assetElement, "size")));
                }
            }

            var publishedAt = DateTimeOffset.TryParse(GetString(element, "published_at"),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : (DateTimeOffset?)null;

            var title = GetString(element, "name");
            releases.Add(new ReleaseInfo(
                tag,
                version,
                string.IsNullOrWhiteSpace(title) ? tag : title,
                GetString(element, "body"),
                GetString(element, "html_url"),
                publishedAt,
                GetBool(element, "draft"),
                GetBool(element, "prerelease"),
                assets));
        }

        return releases;
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool GetBool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    private static long GetInt64(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt64(out var number)
            ? number
            : 0;
}
