namespace Waves.Core.Common.Downloads;

public static class DownloadTask
{
    private const int MaxBufferSize = 65536;
    private const long UpdateThreshold = 1048576;
    private const int MaxRetryCount = 4;

    public static async Task DownloadFileByChunks(
        IHttpClientService httpClientService,
        string url,
        string filePath,
        long start,
        long end,
        bool isLast = false,
        long allSize = 0L,
        DownloadState state = null,
        CancellationTokenSource? downloadCts = default,
        IProgress<(GameContextActionType, bool, long, string, long, long)> progress = null
    )
    {
        ValidateArguments(start, end, end - start, downloadCts);
        await using var fileStream = new FileStream(
            filePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read,
            262144,
            true
        );

        await DownloadRangeWithRetryAsync(
                httpClientService,
                url,
                fileStream,
                filePath,
                start,
                end,
                state,
                downloadCts!.Token,
                progress
            )
            .ConfigureAwait(false);

        if (isLast)
            fileStream.SetLength(allSize);

        await fileStream.FlushAsync(downloadCts.Token).ConfigureAwait(false);
    }

    public static async Task DownloadFileByFull(
        IHttpClientService httpClientService,
        string url,
        long size,
        string filePath,
        IndexChunkInfo chunk,
        DownloadState state = null,
        CancellationTokenSource? downloadCts = default,
        IProgress<(GameContextActionType, bool, long, string, long, long)> progress = null
    )
    {
        ValidateArguments(chunk.Start, chunk.End, size, downloadCts);
        await using var fileStream = new FileStream(
            filePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            262144,
            true
        );

        await DownloadRangeWithRetryAsync(
                httpClientService,
                url,
                fileStream,
                filePath,
                chunk.Start,
                chunk.End,
                state,
                downloadCts!.Token,
                progress
            )
            .ConfigureAwait(false);

        fileStream.SetLength(size);
        await fileStream.FlushAsync(downloadCts.Token).ConfigureAwait(false);
    }

    private static async Task DownloadRangeWithRetryAsync(
        IHttpClientService httpClientService,
        string url,
        FileStream fileStream,
        string filePath,
        long start,
        long end,
        DownloadState? state,
        CancellationToken cancellationToken,
        IProgress<(GameContextActionType, bool, long, string, long, long)>? progress
    )
    {
        long totalSize = end - start + 1;
        long totalWritten = 0;
        long accumulatedBytes = 0;
        int retryCount = 0;

        while (totalWritten < totalSize)
        {
            ThrowIfCanceled(state, cancellationToken);
            long requestStart = start + totalWritten;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new RangeHeaderValue(requestStart, end);

                using var response = await httpClientService
                    .GameDownloadClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                if (IsTransientStatusCode(response.StatusCode))
                {
                    throw new HttpRequestException(
                        $"CDN returned {(int)response.StatusCode} ({response.StatusCode}).",
                        null,
                        response.StatusCode
                    );
                }

                response.EnsureSuccessStatusCode();
                ValidateRangeResponse(response, requestStart, end, totalWritten);

                await using var networkStream = await response
                    .Content.ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);

                fileStream.Seek(requestStart, SeekOrigin.Begin);

                while (totalWritten < totalSize)
                {
                    ThrowIfCanceled(state, cancellationToken);
                    if (state != null)
                        await state.PauseToken.WaitIfPausedAsync().ConfigureAwait(false);

                    int bytesToRead = (int)Math.Min(MaxBufferSize, totalSize - totalWritten);
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(bytesToRead);
                    try
                    {
                        int bytesRead = await ReadWithIdleTimeoutAsync(
                                networkStream,
                                buffer.AsMemory(0, bytesToRead),
                                cancellationToken
                            )
                            .ConfigureAwait(false);

                        if (bytesRead == 0)
                            throw new IOException(
                                $"下载流提前结束：{totalWritten}/{totalSize}，{filePath}"
                            );

                        if (state != null)
                        {
                            await state
                                .SpeedLimiter.LimitAsync(bytesRead, cancellationToken)
                                .ConfigureAwait(false);
                        }

                        await fileStream
                            .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                            .ConfigureAwait(false);

                        totalWritten += bytesRead;
                        accumulatedBytes += bytesRead;

                        if (accumulatedBytes >= UpdateThreshold)
                        {
                            progress?.Report(
                                (
                                    GameContextActionType.Download,
                                    true,
                                    accumulatedBytes,
                                    filePath,
                                    totalWritten,
                                    totalSize
                                )
                            );
                            accumulatedBytes = 0;
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested && state?.IsStop != true)
            {
                await DelayBeforeRetryAsync(
                        ++retryCount,
                        filePath,
                        requestStart,
                        end,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (IsTransient(ex))
            {
                await DelayBeforeRetryAsync(
                        ++retryCount,
                        filePath,
                        requestStart,
                        end,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            catch (IOException)
            {
                await DelayBeforeRetryAsync(
                        ++retryCount,
                        filePath,
                        requestStart,
                        end,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }

        if (accumulatedBytes > 0)
        {
            progress?.Report(
                (
                    GameContextActionType.Download,
                    true,
                    accumulatedBytes,
                    filePath,
                    totalWritten,
                    totalSize
                )
            );
        }
    }

    private static async ValueTask<int> ReadWithIdleTimeoutAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken
    )
    {
        using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        idleCts.CancelAfter(TimeSpan.FromSeconds(90));
        return await stream.ReadAsync(buffer, idleCts.Token).ConfigureAwait(false);
    }

    private static async Task DelayBeforeRetryAsync(
        int retryCount,
        string filePath,
        long requestStart,
        long end,
        CancellationToken cancellationToken
    )
    {
        if (retryCount > MaxRetryCount)
            throw new IOException(
                $"下载重试 {MaxRetryCount} 次后仍然失败：{filePath}，范围 {requestStart}-{end}"
            );

        int baseDelay = Math.Min(1000 * (1 << (retryCount - 1)), 8000);
        int delay = baseDelay + Random.Shared.Next(200, 800);
        Log.Warning(
            "下载中断，{Delay}ms 后进行第 {Retry}/{MaxRetry} 次续传：{FilePath} [{Start}-{End}]",
            delay,
            retryCount,
            MaxRetryCount,
            filePath,
            requestStart,
            end
        );
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateArguments(
        long start,
        long end,
        long size,
        CancellationTokenSource? downloadCts
    )
    {
        if (downloadCts == null)
            throw new ArgumentNullException(nameof(downloadCts));
        if ((start < 0 || end < start) && size != 0)
            throw new ArgumentException($"分片范围无效：{start}-{end}");
        downloadCts.Token.ThrowIfCancellationRequested();
    }

    private static void ThrowIfCanceled(DownloadState? state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (state?.IsStop == true)
            throw new OperationCanceledException(cancellationToken);
    }

    private static void ValidateRangeResponse(
        HttpResponseMessage response,
        long requestStart,
        long requestEnd,
        long totalWritten
    )
    {
        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            var range = response.Content.Headers.ContentRange;
            if (range?.From != requestStart || range.To > requestEnd)
                throw new IOException(
                    $"CDN返回了错误的分片范围：请求 {requestStart}-{requestEnd}，返回 {range}"
                );
            return;
        }

        // 首次完整范围请求兼容忽略 Range、直接返回完整内容的服务器。
        if (totalWritten == 0 && requestStart == 0)
            return;

        throw new IOException(
            $"CDN未响应续传范围 {requestStart}-{requestEnd}，状态码 {(int)response.StatusCode}"
        );
    }

    private static bool IsTransient(HttpRequestException exception)
    {
        return exception.StatusCode == null || IsTransientStatusCode(exception.StatusCode.Value);
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        int code = (int)statusCode;
        return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || code is 500 or 502 or 503 or 504;
    }
}
