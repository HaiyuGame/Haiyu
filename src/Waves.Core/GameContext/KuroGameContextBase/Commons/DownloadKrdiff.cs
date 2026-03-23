using System.Buffers;
using Waves.Core.Common;
using Waves.Core.Models;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext;

partial class KuroGameContextBase
{

    private async Task<int> DecompressKrdiffFile(
        string folder,
        string? krdiffPath,
        int curent,
        int total,
        string? tempFolder = null
    )
    {
        if (krdiffPath == null)
            return -1000;
        DiffDecompressManager manager = new DiffDecompressManager(
            folder,
            tempFolder ?? folder,
            krdiffPath
        );
        IProgress<(double, double)> progress = new Progress<(double, double)>();
        ((Progress<(double, double)>)progress).ProgressChanged += async (s, e) =>
        {
            if (gameContextOutputDelegate == null)
                return;
            await gameContextOutputDelegate
                .Invoke(
                    this,
                    new GameContextOutputArgs
                    {
                        Type = GameContextActionType.Decompress,
                        CurrentSize = (long)e.Item1,
                        TotalSize = (long)e.Item2,
                        DownloadSpeed = 0,
                        VerifySpeed = 0,
                        RemainingTime = TimeSpan.FromMicroseconds(0),
                        IsAction = _downloadState?.IsActive ?? false,
                        IsPause = _downloadState?.IsPaused ?? false,
                        TipMessage = "正在解压合并资源",
                        CurrentDecompressCount = curent,
                        MaxDecompressValue = total,
                    }
                )
                .ConfigureAwait(false);
        };
        var result = await manager.StartAsync(progress);
        Logger.WriteInfo($"解压程序结果{result}");
        return result;
    }



    private async Task<string?> DownloadFileByKrDiff(string dest, string filePath, bool isPred = false)
    {
        long accumulatedBytes = 0;
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
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    _downloadBaseUrl.TrimEnd('/') + "/" + dest.TrimStart('/')
                );
                var downloadCts = GetCTS(isPred);
                var state = GetState(isPred);
                using var response = await HttpClientService
                    .GameDownloadClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        downloadCts.Token
                    )
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var stream = await response
                    .Content.ReadAsStreamAsync(downloadCts.Token)
                    .ConfigureAwait(false);
                long totalWritten = 0;
                long chunkTotalSize = long.Parse(
                    response.Content.Headers.GetValues("Content-Length").First()
                );
                var memoryPool = ArrayPool<byte>.Shared;
                fileStream.Seek(0, SeekOrigin.Begin);
                bool isBreak = false;
                while (totalWritten < chunkTotalSize)
                {
                    if (downloadCts.IsCancellationRequested || state?.IsStop == true)
                    {
                        return null;
                    }
                    if (state != null)
                        await state.PauseToken.WaitIfPausedAsync().ConfigureAwait(false);
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
                        await state.SpeedLimiter.LimitAsync(bytesRead).ConfigureAwait(false);
                    await fileStream
                        .WriteAsync(buffer.AsMemory(0, bytesRead), downloadCts.Token)
                        .ConfigureAwait(false);
                    totalWritten += bytesRead;
                    accumulatedBytes += bytesRead;
                    if (accumulatedBytes >= UpdateThreshold)
                    {
                        await UpdateFileProgress(
                                GameContextActionType.Download,
                                accumulatedBytes,
                                true,
                                false,
                                "下载差异文件"
                            )
                            .ConfigureAwait(false);
                        accumulatedBytes = 0;
                    }
                }
                if (accumulatedBytes > 0 && !isBreak)
                {
                    await UpdateFileProgress(
                            GameContextActionType.Download,
                            accumulatedBytes,
                            true,
                            false,
                            "下载差异文件"
                        )
                        .ConfigureAwait(false);
                }
                if (totalWritten != chunkTotalSize)
                {
                    throw new IOException($"分片写入不完整: {totalWritten}/{chunkTotalSize}");
                }
                await fileStream.FlushAsync();
                return filePath;
            }
            catch (Exception ex)
            {
                Logger.WriteError(ex.Message);
                return null;
            }
            finally
            {
                await fileStream.FlushAsync();
                await fileStream.DisposeAsync();
            }
        }
    }
}
