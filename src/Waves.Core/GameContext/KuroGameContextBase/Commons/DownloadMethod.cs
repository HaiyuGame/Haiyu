using System.Buffers;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext;

/// <summary>
/// 下载方法
/// </summary>
partial class KuroGameContextBase
{
    /// <summary>
    /// 检查单个分片
    /// </summary>
    private async Task<bool> ValidateFileChunks(
        IndexChunkInfo file,
        string filePath,
        bool isPred = false
    )
    {
        using (
            var fs = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                262144,
                true
            )
        )
        {
            try
            {
                //if (fs.Length < file.End + 1) // 检查文件长度是否足够
                //{
                //    Debug.WriteLine($"文件长度不足: {fs.Length} < {file.End + 1}");
                //    return true;
                //}
                var memoryPool = ArrayPool<byte>.Shared;
                var downloadCts = GetCTS(isPred);
                var state = GetState(isPred);
                if (downloadCts == null || state?.IsStop == true)
                {
                    throw new OperationCanceledException();
                }
                long offset = file.Start;
                long remaining = file.End - file.Start + 1;
                bool isValid = true;
                fs.Seek(offset, SeekOrigin.Begin);
                using (var md5 = MD5.Create())
                {
                    long accumulatedBytes = 0L;
                    while (remaining > 0 && isValid)
                    {
                        if (state != null)
                            await state.PauseToken.WaitIfPausedAsync();
                        var buffer = memoryPool.Rent(MaxBufferSize);
                        try
                        {
                            if (
                                downloadCts.IsCancellationRequested
                                || state?.IsStop == true
                            )
                            {
                                throw new OperationCanceledException();
                            }
                            int bytesRead = await fs.ReadAsync(
                                    buffer,
                                    0,
                                    MaxBufferSize,
                                    downloadCts.Token
                                )
                                .ConfigureAwait(false);
                            if (bytesRead == 0)
                            {
                                break;
                            }
                            md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                            remaining -= bytesRead;
                            accumulatedBytes += bytesRead;
                            if (accumulatedBytes >= UpdateThreshold)
                            {
                                await UpdateFileProgress(
                                        GameContextActionType.Verify,
                                        accumulatedBytes,
                                        false,
                                        isPred
                                    )
                                    .ConfigureAwait(false);
                                accumulatedBytes = 0;
                            }
                        }
                        catch (IOException ex)
                        {
                            Logger.WriteError(ex.Message);
                        }
                        finally
                        {
                            memoryPool.Return(buffer);
                        }
                    }
                    if (accumulatedBytes > 0 && accumulatedBytes < UpdateThreshold)
                    {
                        await UpdateFileProgress(
                                GameContextActionType.Verify,
                                accumulatedBytes,
                                false,
                                isPred
                            )
                            .ConfigureAwait(false);
                    }
                    md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    string hash = BitConverter.ToString(md5.Hash!).Replace("-", "").ToLower();
                    isValid = hash == file.Md5.ToLower();
                    Logger.WriteInfo($"分片校验结果{hash}|{file.Md5}");
                    return !isValid;
                }
            }
            catch (IOException ex)
            {
                Logger.WriteError(ex.Message);
                return false;
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException();
            }
            finally
            {
                fs.Close();
                fs.Dispose();
            }
        }
    }

    /// <summary>
    /// 检查整个文件
    /// </summary>
    /// <exception cref="OperationCanceledException"></exception>
    private async Task<bool> VaildateFullFile(string md5Value, string filePath, bool isPred = false)
    {
        const int bufferSize = 262144;
        using var md5 = MD5.Create();
        var memoryPool = ArrayPool<byte>.Shared;
        var downloadCts = GetCTS(isPred);
        var state = GetState(isPred);
        if (downloadCts == null || state?.IsStop == true)
        {
            throw new OperationCanceledException();
        }
        const long UpdateThreshold = 1048576;
        FileStream? fs = null;
        try
        {
            using (
                fs = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: bufferSize,
                    true
                )
            )
            {
                bool isBreak = false;
                long accumulatedBytes = 0L;
                while (true)
                {
                    if (downloadCts.IsCancellationRequested || state?.IsStop == true)
                    {
                        throw new OperationCanceledException();
                    }
                    //暂停锁
                    if (state != null)
                        await state.PauseToken.WaitIfPausedAsync().ConfigureAwait(false);
                    byte[] buffer = memoryPool.Rent(bufferSize);
                    try
                    {
                        int bytesRead = await fs.ReadAsync(
                                buffer.AsMemory(0, bufferSize),
                                downloadCts.Token
                            )
                            .ConfigureAwait(false);
                        if (bytesRead == 0)
                        {
                            isBreak = true;
                            break;
                        }
                        md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                        accumulatedBytes += bytesRead; // 添加此行以累加字节数
                        if (accumulatedBytes >= UpdateThreshold)
                        {
                            await UpdateFileProgress(
                                    GameContextActionType.Verify,
                                    accumulatedBytes,
                                    false,
                                    isPred
                                )
                                .ConfigureAwait(false);
                            accumulatedBytes = 0;
                        }
                    }
                    finally
                    {
                        memoryPool.Return(buffer);
                    }
                }
                if (accumulatedBytes < UpdateThreshold)
                {
                    await UpdateFileProgress(
                            GameContextActionType.Verify,
                            accumulatedBytes,
                            false,
                            isPred: isPred
                        )
                        .ConfigureAwait(false);
                }
            }

            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            string hash = BitConverter.ToString(md5.Hash!).Replace("-", "").ToLower();

            return !(hash == md5Value);
        }
        catch (OperationCanceledException)
        {
            throw new OperationCanceledException();
        }
        catch (IOException ex)
        {
            Logger.WriteError(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            Logger.WriteError(ex.Message);
            return false;
        }
        finally
        {
            fs?.Close();
            fs?.Dispose();
        }
    }

    /// <summary>
    /// 开始下载分片
    /// </summary>
    private async Task DownloadFileByChunks(
        string dest,
        string filePath,
        long start,
        long end,
        bool isLast = false,
        long allSize = 0L,
        bool isPred = false
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
                var downloadCts = GetCTS(isPred);
                var state = GetState(isPred);
                if (downloadCts == null || state?.IsStop == true)
                {
                    throw new OperationCanceledException();
                }
                long accumulatedBytes = 0;
                if (start == 0 && end == -1)
                {
                    Logger.WriteError($"文件{filePath}，分片数据错误，start={start},end={end}");
                }
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    _downloadBaseUrl.TrimEnd('/') + "/" + dest.TrimStart('/')
                );
                request.Headers.Range = new RangeHeaderValue(start, end);
                using var response = await HttpClientService.GameDownloadClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    downloadCts.Token
                );
                var stream = await response.Content.ReadAsStreamAsync(downloadCts.Token);
                if (start < 0 || end < start)
                {
                    Logger.WriteError($"分片范围无效: {start}-{end}");
                    throw new ArgumentException($"分片范围无效: {start}-{end}");
                }

                long totalWritten = 0;
                long chunkTotalSize = end - start + 1;
                var memoryPool = ArrayPool<byte>.Shared;
                fileStream.Seek(start, SeekOrigin.Begin);
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
                                .SpeedLimiter.LimitAsync(bytesRead)
                                .ConfigureAwait(false);
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
                                    isPred
                                )
                                .ConfigureAwait(false);
                            accumulatedBytes = 0;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteError(ex.Message);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                if (accumulatedBytes > 0 && !isBreak)
                {
                    await UpdateFileProgress(
                            GameContextActionType.Download,
                            accumulatedBytes,
                            isPred: isPred
                        )
                        .ConfigureAwait(false);
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
                await fileStream.FlushAsync(_downloadCTS?.Token ?? CancellationToken.None);
                await fileStream.FlushAsync();
                await fileStream.DisposeAsync();
            }
        }
    }


    /// <summary>
    /// 检查指定列表的Md5校验值
    /// </summary>
    /// <param name="list"></param>
    /// <param name="folder"></param>
    /// <param name="tempFolder"></param>
    /// <param name="newFiles"></param>
    /// <returns></returns>
    private async Task<bool> CheckApplyFilesMd5(
        List<IndexResource> list,
        string folder,
        string tempFolder,
        Dictionary<string, string> newFiles
    )
    {
        try
        {
            var keys = newFiles.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                string value = newFiles[key];
                if (File.Exists(value))
                    File.Delete(value);
                File.Move(key, value);
                this.gameContextOutputDelegate?.Invoke(
                        this,
                        new GameContextOutputArgs()
                        {
                            Type = GameContextActionType.DeleteFile,
                            FileTotal = keys.Count,
                            CurrentFile = i,
                            DeleteString = $"正在移动校验文件{System.IO.Path.GetFileName(value)}",
                        }
                    )
                    .ConfigureAwait(false);
            }
            await InitializeProgress(list);
            var resource = await this.GetGameLauncherSourceAsync();
            var resourceOne = await this.GetGameResourceAsync(resource.ResourceDefault);
            if (!await GetGameResourceAsync(folder, resource, false))
            {
                await UpdateFileProgress(
                        GameContextActionType.TipMessage,
                        0,
                        false,
                        false,
                        "更新校验出错，请直接尝试修复游戏，下载缓存需手动删除"
                    )
                    .ConfigureAwait(false);
                await SetNoneStatusAsync();
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            await SetNoneStatusAsync().ConfigureAwait(false);
            await UpdateFileProgress(GameContextActionType.TipMessage, 0, false, false, ex.Message);
            this._isDownload = false;
            Logger.WriteError(ex.Message);
            return false;
        }
    }


    /// <summary>
    /// 校验整个文件
    /// </summary>
    private async Task DownloadFileByFull(
        string dest,
        long size,
        string filePath,
        IndexChunkInfo chunk,
        bool isPred = false
    )
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
                if (chunk.Start == 0 && chunk.End == -1)
                {
                    Logger.WriteError(
                        $"文件{filePath}，分片数据错误，start={chunk.Start},end={chunk.End}"
                    );
                    return;
                }
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    _downloadBaseUrl.TrimEnd('/') + "/" + dest.TrimStart('/')
                );
                request.Headers.Range = new RangeHeaderValue(chunk.Start, chunk.End);
                var downloadCts = GetCTS(isPred);
                var state = GetState(isPred);
                using var response = await HttpClientService
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
                    Logger.WriteError($"分片范围无效，start={chunk.Start},end={chunk.End}");
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
                                isPred
                            )
                            .ConfigureAwait(false);
                        accumulatedBytes = 0; // 重置累积计数器
                    }
                }
                if (accumulatedBytes > 0 && !isBreak)
                {
                    await UpdateFileProgress(
                            GameContextActionType.Download,
                            accumulatedBytes,
                            true,
                            isPred
                        )
                        .ConfigureAwait(false);
                }
                if (totalWritten != chunkTotalSize)
                {
                    throw new IOException($"分片写入不完整: {totalWritten}/{chunkTotalSize}");
                }
                fileStream.SetLength(size);
            }
            catch (Exception ex)
            {
                Logger.WriteError($"下载文件{filePath}出现异常" + ex.Message);
            }
            finally
            {
                await fileStream.FlushAsync().ConfigureAwait(false);
                await fileStream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

}
