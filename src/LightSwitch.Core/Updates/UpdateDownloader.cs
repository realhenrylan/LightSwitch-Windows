using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Cryptography;

namespace LightSwitch.Core.Updates;

public interface IUpdateFileDownloader
{
    Task<UpdateDownloadResult> DownloadAsync(ReleaseAsset asset, ReleaseAsset checksumAsset,
        string destinationDirectory, IProgress<double>? progress, CancellationToken cancellationToken);
}

/// <summary>
/// Downloads a release asset over HTTPS and verifies it against the sibling
/// <c>.sha256</c> asset published by the release workflow before exposing a path.
/// A file that fails verification is deleted and never returned to the caller.
/// </summary>
public sealed class UpdateDownloader : IUpdateFileDownloader, IDisposable
{
    private const int BufferSize = 81920;
    private const int MaxChecksumBytes = 4096;

    private readonly HttpClient _httpClient;
    private readonly UpdateCheckOptions _options;

    public UpdateDownloader(UpdateCheckOptions? options = null, HttpMessageHandler? handler = null)
    {
        _options = options ?? new UpdateCheckOptions();
        _httpClient = new HttpClient(handler ?? new HttpClientHandler())
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
    }

    public async Task<UpdateDownloadResult> DownloadAsync(ReleaseAsset asset, ReleaseAsset checksumAsset,
        string destinationDirectory, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!TryGetHttpsUri(asset.DownloadUrl, out var assetUri) ||
            !TryGetHttpsUri(checksumAsset.DownloadUrl, out var checksumUri))
        {
            return UpdateDownloadResult.Fail(UpdateDownloadStatus.InvalidAsset,
                "更新包下载地址不是受信任的 HTTPS GitHub 地址。");
        }

        var fileName = Path.GetFileName(asset.Name);
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(fileName, asset.Name, StringComparison.Ordinal) ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return UpdateDownloadResult.Fail(UpdateDownloadStatus.InvalidAsset,
                "更新包文件名无效。");
        }

        string? partialPath = null;
        try
        {
            Directory.CreateDirectory(destinationDirectory);
            var finalPath = Path.Combine(destinationDirectory, fileName);
            partialPath = finalPath + ".part";

            var (checksumText, checksumFailure) = await FetchTextAsync(checksumUri, cancellationToken)
                .ConfigureAwait(false);
            if (checksumFailure is not null)
            {
                return checksumFailure;
            }

            var expectedHash = ParseSha256(checksumText);
            if (expectedHash is null)
            {
                return UpdateDownloadResult.Fail(UpdateDownloadStatus.InvalidResponse,
                    "SHA256 校验文件格式无效，已取消更新。");
            }

            if (File.Exists(finalPath))
            {
                var existingHash = await ComputeFileHashAsync(finalPath, cancellationToken).ConfigureAwait(false);
                if (string.Equals(existingHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report(1);
                    return new UpdateDownloadResult(UpdateDownloadStatus.Succeeded, finalPath,
                        "更新包已存在且 SHA256 校验通过。", expectedHash, existingHash);
                }
            }

            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(_options.DownloadTimeout);
                using var request = new HttpRequestMessage(HttpMethod.Get, assetUri);
                using var response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                    .ConfigureAwait(false);

                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                {
                    return UpdateDownloadResult.Fail(UpdateDownloadStatus.RateLimited,
                        "下载更新包时被 GitHub 限流，请稍后重试。");
                }

                if (!response.IsSuccessStatusCode)
                {
                    return UpdateDownloadResult.Fail(UpdateDownloadStatus.HttpError,
                        $"下载更新包失败：HTTP {(int)response.StatusCode}。");
                }

                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength > _options.MaxDownloadBytes)
                {
                    return UpdateDownloadResult.Fail(UpdateDownloadStatus.TooLarge,
                        "更新包超过允许的大小，已取消下载。");
                }

                await using var source = await response.Content.ReadAsStreamAsync(timeout.Token)
                    .ConfigureAwait(false);
                await using var destination = new FileStream(partialPath, FileMode.Create, FileAccess.Write,
                    FileShare.None, BufferSize, useAsync: true);

                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[BufferSize];
                long total = 0;

                while (true)
                {
                    var read = await source.ReadAsync(buffer, timeout.Token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    total += read;
                    if (total > _options.MaxDownloadBytes)
                    {
                        return UpdateDownloadResult.Fail(UpdateDownloadStatus.TooLarge,
                            "更新包超过允许的大小，已取消下载。");
                    }

                    hash.AppendData(buffer, 0, read);
                    await destination.WriteAsync(buffer.AsMemory(0, read), timeout.Token).ConfigureAwait(false);

                    if (contentLength is > 0)
                    {
                        progress?.Report(Math.Clamp((double)total / contentLength.Value, 0, 1));
                    }
                }

                await destination.FlushAsync(timeout.Token).ConfigureAwait(false);
                var actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();

                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    return UpdateDownloadResult.Fail(UpdateDownloadStatus.ChecksumMismatch,
                        "更新包 SHA256 校验失败，已删除下载文件。", expectedHash, actualHash);
                }

                destination.Close();
                File.Move(partialPath, finalPath, overwrite: true);
                partialPath = null;
                progress?.Report(1);
                return new UpdateDownloadResult(UpdateDownloadStatus.Succeeded, finalPath,
                    "更新包下载完成，SHA256 校验通过。", expectedHash, actualHash);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return UpdateDownloadResult.Fail(UpdateDownloadStatus.Canceled, "已取消下载更新。");
        }
        catch (OperationCanceledException)
        {
            return UpdateDownloadResult.Fail(UpdateDownloadStatus.Timeout,
                "下载更新超时，请检查网络后重试。");
        }
        catch (HttpRequestException ex)
        {
            Log.Warn($"Update download failed: {ex.Message}");
            return UpdateDownloadResult.Fail(UpdateDownloadStatus.NetworkError,
                "下载更新时无法连接 GitHub，请检查网络后重试。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Log.Warn($"Update download failed to write file: {ex.Message}");
            return UpdateDownloadResult.Fail(UpdateDownloadStatus.IoError,
                $"下载更新失败：{ex.Message}");
        }
        finally
        {
            if (partialPath is not null)
            {
                try
                {
                    File.Delete(partialPath);
                }
                catch
                {
                    // Best effort only; the .part suffix keeps it out of the installer flow.
                }
            }
        }
    }

    public void Dispose() => _httpClient.Dispose();

    private async Task<(string? Text, UpdateDownloadResult? Failure)> FetchTextAsync(Uri uri,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.CheckTimeout);

        try
        {
            using var response = await _httpClient
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return (null, UpdateDownloadResult.Fail(
                    response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
                        ? UpdateDownloadStatus.RateLimited
                        : UpdateDownloadStatus.HttpError,
                    $"获取 SHA256 校验文件失败：HTTP {(int)response.StatusCode}。"));
            }

            if (response.Content.Headers.ContentLength is > MaxChecksumBytes)
            {
                return (null, UpdateDownloadResult.Fail(UpdateDownloadStatus.InvalidResponse,
                    "SHA256 校验文件过大，已取消更新。"));
            }

            var text = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            if (text.Length > MaxChecksumBytes)
            {
                return (null, UpdateDownloadResult.Fail(UpdateDownloadStatus.InvalidResponse,
                    "SHA256 校验文件过大，已取消更新。"));
            }

            return (text, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return (null, UpdateDownloadResult.Fail(UpdateDownloadStatus.Canceled, "已取消下载更新。"));
        }
        catch (OperationCanceledException)
        {
            return (null, UpdateDownloadResult.Fail(UpdateDownloadStatus.Timeout,
                "获取 SHA256 校验文件超时，请稍后重试。"));
        }
        catch (HttpRequestException)
        {
            return (null, UpdateDownloadResult.Fail(UpdateDownloadStatus.NetworkError,
                "无法下载 SHA256 校验文件，请检查网络后重试。"));
        }
        catch (IOException ex)
        {
            return (null, UpdateDownloadResult.Fail(UpdateDownloadStatus.NetworkError,
                $"无法读取 SHA256 校验文件：{ex.Message}"));
        }
    }

    private static string? ParseSha256(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var token = content.Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (token is not { Length: 64 })
        {
            return null;
        }

        return token.All(Uri.IsHexDigit) ? token.ToLowerInvariant() : null;
    }

    private static async Task<string> ComputeFileHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            BufferSize, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool TryGetHttpsUri(string value, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var candidate) ||
            candidate.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        // Release assets are served by github.com and redirected to its CDN.
        var host = candidate.Host;
        if (!host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
            !host.Equals("githubusercontent.com", StringComparison.OrdinalIgnoreCase) &&
            !host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = candidate;
        return true;
    }
}
