using System.Buffers;
using System.Collections;
using System.IO.Compression;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Waves.Core.Common.Downloads;

public static class UnZipTask
{
    const int MaxBufferSize = 65536;
    const long UpdateThreshold = 1048576;

    public static async Task<bool> UnZipFileAsync(
        string zipFile,
        string tempFolder,
        long maxSize,
        DownloadState state = null,
        IProgress<(GameContextActionType, bool, long, string, long, long)> progress = null,
        LoggerService logger= null
    )
    {
        try
        {
            if (
                string.IsNullOrWhiteSpace(zipFile)
                || string.IsNullOrWhiteSpace(tempFolder)
                || maxSize == 0
            )
            {
                return false;
            }
            using var fs = new FileStream(
                zipFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                true
            );
            using var archive = new ZipArchive(fs, ZipArchiveMode.Read);
            var currentBytes = 0L;
            foreach (var entry in archive.Entries)
            {
                var isDirectory =
                    string.IsNullOrEmpty(entry.Name)
                    || entry.FullName.EndsWith("/", StringComparison.Ordinal)
                    || entry.FullName.EndsWith("\\", StringComparison.Ordinal);

                if (isDirectory)
                {
                    var dirRelativePath = entry
                        .FullName.Replace('/', Path.DirectorySeparatorChar)
                        .Replace('\\', Path.DirectorySeparatorChar);

                    var dirFullPath = Path.Combine(tempFolder, dirRelativePath);
                    Directory.CreateDirectory(dirFullPath);
                    continue;
                }
                var fileRelativePath = entry
                    .FullName.Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                var fileFullPath = Path.Combine(tempFolder, fileRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(fileFullPath)!);
                using var entryStream = await entry.OpenAsync();
                using var fileStream = new FileStream(
                    fileFullPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    true
                );
                // 预先分配文件大小以避免磁盘碎片并提升写入性能
                fileStream.SetLength(entry.Length);
                long totalWritten = 0;
                long chunkTotalSize = entry.Length;
                long accumulatedBytes = 0;
                bool isBreak = false;
                while (totalWritten < chunkTotalSize)
                {
                    int bytesToRead = (int)Math.Min(MaxBufferSize, chunkTotalSize - totalWritten);
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(bytesToRead);
                    try
                    {
                        int bytesRead = await entryStream
                            .ReadAsync(buffer.AsMemory(0, bytesToRead))
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
                            .WriteAsync(buffer.AsMemory(0, bytesRead))
                            .ConfigureAwait(false);
                        totalWritten += bytesRead;
                        accumulatedBytes += bytesRead;
                        currentBytes += bytesRead;
                        if (accumulatedBytes >= UpdateThreshold)
                        {
                            //增加到进度，当前读取字节，文件路径，当前总字节，最大字节
                            progress?.Report((
                                GameContextActionType.Decompress,
                                true,
                                accumulatedBytes,
                                fileFullPath,
                                totalWritten,
                                chunkTotalSize
                            ));
                            accumulatedBytes = 0;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger?.WriteError(ex.Message);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                if (accumulatedBytes > 0 && !isBreak)
                {
                    progress?.Report((
                        GameContextActionType.ZipDecompress,
                        true,
                        accumulatedBytes,
                        fileFullPath,
                        totalWritten,
                        chunkTotalSize
                    ));
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            logger?.WriteError(ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 获得压缩存档中子项解压后大小
    /// </summary>
    /// <param name="zipFile"></param>
    /// <returns></returns>
    public static async Task<long> GetZipEntriesSizeAsync(string zipFile)
    {
        return await Task.Run(() =>
        {
            long size = 0;
            using (var zip = new ZipArchive(File.OpenRead(zipFile)))
            {
                var entries = zip.Entries;
                long totalSize = entries.Sum(e => e.Length);
                size = totalSize;
            }
            return size;
        });
    }
}
