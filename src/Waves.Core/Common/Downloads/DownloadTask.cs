using Serilog.Core;
using System.Buffers;
using System.Net.Http.Headers;
using Waves.Core.Contracts;
using Waves.Core.Contracts.Events;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Waves.Core.Common.Downloads;

public static class DownloadTask
{
    const int MaxBufferSize = 65536;
    const long UpdateThreshold = 1048576;
    /// <summary>
    /// 开始下载分片
    /// </summary>
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
        IProgress<(GameContextActionType,bool,long,string,long,long)> progress = null
    )
    {
        using (
            var fileStream = new FileStream(
                filePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                262144,
                true
            )
        )
        {
            try
            {
                if (downloadCts == null || downloadCts.IsCancellationRequested || state?.IsStop == true)
                {
                    throw new OperationCanceledException();
                }
                long accumulatedBytes = 0;
                long currentBytes = 0;
                if (start == 0 && end == -1)
                {
                    //Logger.WriteError($"文件{filePath}，分片数据错误，start={start},end={end}");
                }
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    url
                );
                request.Headers.Range = new RangeHeaderValue(start, end);
                using var response = await httpClientService.GameDownloadClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    downloadCts.Token
                );
                var stream = await response.Content.ReadAsStreamAsync(downloadCts.Token);
                if (start < 0 || end < start)
                {
                    throw new ArgumentException($"分片范围无效: {start}-{end}");
                }

                long totalWritten = 0;
                long chunkTotalSize = end - start + 1;
                var memoryPool = ArrayPool<byte>.Shared;
                fileStream.Seek(start, SeekOrigin.Begin);
                bool isBreak = false;
                while (totalWritten < chunkTotalSize)
                {
                    if (downloadCts == null || downloadCts.IsCancellationRequested || state?.IsStop == true)
                    {
                        throw new OperationCanceledException();
                    }
                    if (state != null)
                        await state.PauseToken.WaitIfPausedAsync().ConfigureAwait(false); // 暂停检查也异步化
                    int bytesToRead = (int)Math.Min(MaxBufferSize, chunkTotalSize - totalWritten);
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(bytesToRead);
                    try
                    {
                        int bytesRead = await stream
                            .ReadAsync(buffer.AsMemory(0, bytesToRead), downloadCts.Token)
                            .ConfigureAwait(false);
                        if (bytesRead == 0)
                        {
                            isBreak = true;
                            break;
                        }
                        if (state != null)
                            await state
                                .SpeedLimiter.LimitAsync(bytesRead,downloadCts.Token)
                                .ConfigureAwait(false);
                        await fileStream
                            .WriteAsync(buffer.AsMemory(0, bytesRead), downloadCts.Token)
                            .ConfigureAwait(false);
                        totalWritten += bytesRead;
                        accumulatedBytes += bytesRead;
                        currentBytes+= bytesRead;
                        if (accumulatedBytes >= UpdateThreshold)
                        {
                            progress?.Report((GameContextActionType.Download,true,accumulatedBytes,filePath,currentBytes, chunkTotalSize));
                            accumulatedBytes = 0;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        //Logger.WriteError(ex.Message);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                if (accumulatedBytes > 0 && !isBreak)
                {
                    progress?.Report((GameContextActionType.Download, true, accumulatedBytes, filePath, currentBytes, chunkTotalSize));
                }
                if (isLast)
                    fileStream.SetLength(allSize);
                stream.Close();
                await stream.DisposeAsync();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                await fileStream.FlushAsync(downloadCts!=null? downloadCts.Token:default);
                await fileStream.FlushAsync();
                await fileStream.DisposeAsync();
            }
        }
    }


    /// <summary>
    /// 校验整个文件
    /// </summary>
    public static async Task DownloadFileByFull(
        IHttpClientService httpClientService,
        string url,
        long size,
        string filePath,
        IndexChunkInfo chunk,
        DownloadState state = null,
        CancellationTokenSource? downloadCts = default,
        IProgress<(GameContextActionType, bool, long,string,long,long)> progress = null
    )
    {
        long accumulatedBytes = 0;
        long currentByte = 0;
        using (
            var fileStream = new FileStream(
                filePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                262144,
                true
            )
        )
        {
            try
            {
                if (chunk.Start == 0 && chunk.End == -1)
                {
                    //Logger.WriteError(
                    //    $"文件{filePath}，分片数据错误，start={chunk.Start},end={chunk.End}"
                    //);
                    return;
                }
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    url
                );
                request.Headers.Range = new RangeHeaderValue(chunk.Start, chunk.End);
                using var response = await httpClientService
                    .GameDownloadClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        downloadCts.Token
                    )
                    .ConfigureAwait(false); // 非UI上下文切换

                response.EnsureSuccessStatusCode();
                var stream = await response
                    .Content.ReadAsStreamAsync(downloadCts.Token)
                    .ConfigureAwait(false);
                if (chunk.Start < 0 || chunk.End < chunk.Start)
                {
                    //Logger.WriteError($"分片范围无效，start={chunk.Start},end={chunk.End}");
                    throw new ArgumentException($"分片范围无效: {chunk.Start}-{chunk.End}");
                }

                long totalWritten = 0;
                long chunkTotalSize = chunk.End - chunk.Start + 1;
                var memoryPool = ArrayPool<byte>.Shared;

                fileStream.Seek(chunk.Start, SeekOrigin.Begin);
                bool isBreak = false;
                while (totalWritten < chunkTotalSize)
                {
                    if (downloadCts.IsCancellationRequested || state?.IsStop == true)
                    {
                        throw new OperationCanceledException();
                    }
                    if (state != null)
                        await state.PauseToken.WaitIfPausedAsync().ConfigureAwait(false); // 暂停检查也异步化
                    int bytesToRead = (int)Math.Min(MaxBufferSize, chunkTotalSize - totalWritten);
                    byte[] buffer = memoryPool.Rent(bytesToRead);
                    int bytesRead = await stream
                        .ReadAsync(buffer.AsMemory(0, bytesToRead), downloadCts.Token)
                        .ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        isBreak = true;
                    }
                    if (state != null)
                        await state.SpeedLimiter.LimitAsync(bytesRead, downloadCts.Token).ConfigureAwait(false);
                    await fileStream
                        .WriteAsync(buffer.AsMemory(0, bytesRead), downloadCts.Token)
                        .ConfigureAwait(false);
                    totalWritten += bytesRead;
                    accumulatedBytes += bytesRead;
                    currentByte += bytesRead;
                    if (accumulatedBytes >= UpdateThreshold)
                    {
                        progress?.Report((GameContextActionType.Download,true,accumulatedBytes,filePath,currentByte, chunkTotalSize));
                        accumulatedBytes = 0;
                    }
                }
                if (accumulatedBytes > 0 && !isBreak)
                {
                    progress?.Report((GameContextActionType.Download, true, accumulatedBytes, filePath, currentByte, chunkTotalSize));
                }
                if (totalWritten != chunkTotalSize)
                {
                    throw new IOException($"分片写入不完整: {totalWritten}/{chunkTotalSize}");
                }
                fileStream.SetLength(size);
            }
            catch (Exception ex)
            {
                //Logger.WriteError($"下载文件{filePath}出现异常" + ex.Message);
            }
            finally
            {
                await fileStream.FlushAsync().ConfigureAwait(false);
                await fileStream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
