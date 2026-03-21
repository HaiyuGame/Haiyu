using Serilog.Core;
using System.Buffers;
using System.Security.Cryptography;
using Waves.Core.Contracts.Events;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;

namespace Waves.Core.Common.Downloads;

public static class VerifyTask
{
    const int MaxBufferSize = 65536;

    const long UpdateThreshold = 1048576;

    /// <summary>
    /// 检查单个分片
    /// </summary>
    public static async Task<bool> ValidateFileChunksAsync(
        IndexChunkInfo file,
        string filePath,
        DownloadState state = null,
        CancellationTokenSource? downloadCts = default,
        IProgress<(GameContextActionType,bool,long)> progress=null
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
                var memoryPool = ArrayPool<byte>.Shared;
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
                            if (downloadCts.IsCancellationRequested || state?.IsStop == true)
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
                                //await UpdateFileProgress(
                                //        GameContextActionType.Verify,
                                //        accumulatedBytes,
                                //        false,
                                //        isPred
                                //    )
                                //    .ConfigureAwait(false);
                                progress?.Report((GameContextActionType.Verify,false,accumulatedBytes));
                                accumulatedBytes = 0;
                            }
                        }
                        catch (IOException ex) { }
                        finally
                        {
                            memoryPool.Return(buffer);
                        }
                    }
                    if (accumulatedBytes > 0 && accumulatedBytes < UpdateThreshold)
                    {
                        //await UpdateFileProgress(
                        //        GameContextActionType.Verify,
                        //        accumulatedBytes,
                        //        false,
                        //        isPred
                        //    )
                        //    .ConfigureAwait(false);

                        progress?.Report((GameContextActionType.Verify, false, accumulatedBytes));
                    }
                    md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    string hash = BitConverter.ToString(md5.Hash!).Replace("-", "").ToLower();
                    isValid = hash == file.Md5.ToLower();
                    return !isValid;
                }
            }
            catch (IOException ex)
            {
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
    public static async Task<bool> VaildateFullFile(
        string md5Value,
        string filePath,
        DownloadState state = null,
        CancellationTokenSource? downloadCts = default,
        IProgress<(GameContextActionType, bool, long)> progress = null
    )
    {
        const int bufferSize = 262144;
        using var md5 = MD5.Create();
        var memoryPool = ArrayPool<byte>.Shared;
        if (downloadCts == null || state?.IsStop == true)
        {
            throw new OperationCanceledException();
        }
        const long UpdateThreshold = 1048576;
        try
        {
            using (
                var fs = new FileStream(
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
                            progress?.Report((GameContextActionType.Verify, false, accumulatedBytes));
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
                    progress?.Report((GameContextActionType.Verify, false, accumulatedBytes));
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
            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public static async Task<bool> ValidateFileChunks(
        IndexChunkInfo file,
        string filePath,
        DownloadState state = null,
        CancellationTokenSource? downloadCts = default,
        IProgress<(GameContextActionType, bool, long)> progress = null
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
                var memoryPool = ArrayPool<byte>.Shared;
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
                            if (downloadCts.IsCancellationRequested || state?.IsStop == true)
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
                                progress?.Report((GameContextActionType.Verify, false, accumulatedBytes));
                                accumulatedBytes = 0;
                            }
                        }
                        catch (IOException ex)
                        {
                            //Logger.WriteError(ex.Message);
                        }
                        finally
                        {
                            memoryPool.Return(buffer);
                        }
                    }
                    if (accumulatedBytes > 0 && accumulatedBytes < UpdateThreshold)
                    {
                        progress?.Report((GameContextActionType.Verify, false, accumulatedBytes));
                    }
                    md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    string hash = BitConverter.ToString(md5.Hash!).Replace("-", "").ToLower();
                    isValid = hash == file.Md5.ToLower();
                    //Logger.WriteInfo($"分片校验结果{hash}|{file.Md5}");
                    return !isValid;
                }
            }
            catch (IOException ex)
            {
                //Logger.WriteError(ex.Message);
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
}
