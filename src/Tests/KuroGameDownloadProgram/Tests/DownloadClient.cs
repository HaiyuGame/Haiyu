using Serilog.Core;
using System.Buffers;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Waves.Api.Models.Communitys.DataCenter;
using Waves.Core.Common;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace KuroGameDownloadProgram.Tests;

public class DownloadClient
{
    const int MaxBufferSize = 65536; // 64KB缓冲区
    const long UpdateThreshold = 1048576; // 1MB进度更新阈值

    public static async Task StartTest()
    {
        DownloadClient downloadClient = new();
        var resource = await downloadClient.GetVersionResource("https://pcdownload-aliyun.aki-game.com/launcher/game/G152/10003/3.3.0/LwvQueHvaDihmfrFKvPkzsBMsZoMxIAD/resource/10003/3.3.0/indexFile.json");

        downloadClient.InitDownload(resource, "https://pcdownload-aliyun.aki-game.com/launcher/game/G152/10003/3.3.0/LwvQueHvaDihmfrFKvPkzsBMsZoMxIAD/zip/", "D:\\WutheringWavesGame");
        await downloadClient.WaitDownloadAsync();
    }

    public DownloadClient()
    {
        HttpClient = new HttpClient();
    }
    public HttpClient HttpClient { get; set; }
    public IndexGameResource Resource { get; private set; }
    public string? BaseUrl { get; private set; }
    public string? BaseFolder { get; private set; }

    public async Task<IndexGameResource?> GetVersionResource(string url)
    {
        return await (await HttpClient.GetAsync(url)).Content.ReadFromJsonAsync<IndexGameResource>(IndexGameResourceContext.Default.IndexGameResource) ?? null;
    }

    internal void InitDownload(IndexGameResource? Resource, string? baseUrl, string? folder)
    {
        this.Resource = Resource;
        this.BaseUrl = baseUrl;
        this.BaseFolder = folder;
    }

    public async Task<bool> WaitDownloadAsync()
    {
        try
        {
            // 限制并发数为 4（根据你的网络环境调整）
            var options = new ParallelOptions { MaxDegreeOfParallelism = 4};
            await Parallel.ForEachAsync(Resource.Resource, options, async (item, ct) =>
            {
                var filePath = BuildFilePath(BaseFolder, item);
                Console.WriteLine($"开始处理文件: {item.Dest}");

                if (File.Exists(filePath))
                {
                    if (item.ChunkInfos == null)
                    {
                        var checkResult = await VaildateFullFile(item.Md5, filePath);
                        if (checkResult)
                        {
                            await DownloadFileByFull(
                                item.Dest,
                                item.Size,
                                filePath,
                                new IndexChunkInfo
                                {
                                    Start = 0,
                                    End = item.Size - 1,
                                    Md5 = item.Md5
                                }
                            );
                        }
                    }
                    else
                    {
                        for (int c = 0; c < item.ChunkInfos.Count; c++)
                        {
                            var needDownload = await ValidateFileChunks(item.ChunkInfos[c], filePath);
                            if (needDownload)
                            {
                                long chunkSize = item.ChunkInfos[c].End - item.ChunkInfos[c].Start + 1;


                                if (c == item.ChunkInfos.Count - 1)
                                {
                                    await DownloadFileByChunks(
                                        item.Dest,
                                        filePath,
                                        item.ChunkInfos[c].Start,
                                        item.ChunkInfos[c].End,
                                        true,
                                        item.Size
                                    );
                                }
                                else
                                {
                                    await DownloadFileByChunks(
                                        item.Dest,
                                        filePath,
                                        item.ChunkInfos[c].Start,
                                        item.ChunkInfos[c].End,
                                        false
                                    );
                                }
                            }
                        }
                    }
                }
                else
                {
                    await DownloadFileByFull(
                        item.Dest,
                        item.Size,
                        filePath,
                        new IndexChunkInfo
                        {
                            Start = 0,
                            End = item.Size - 1,
                            Md5 = item.Md5
                        }
                    );
                }
            });
            return true;
        }
        catch (IOException ex)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
        finally
        {
        }
    }

    private string BuildFilePath(string folder, IndexResource file)
    {
        var path = Path.Combine(folder, file.Dest.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new Exception($"文件{file.Dest}创建失败"));
        return path;
    }

    private async Task DownloadFileByFull(
        string dest,
        long size,
        string filePath,
        IndexChunkInfo chunk
    )
    {
        try
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
                if (chunk.Start == 0 && chunk.End == -1)
                {
                    Console.WriteLine(
                        $"文件{filePath}，分片数据错误，start={chunk.Start},end={chunk.End}"
                    );
                    return;
                }
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    BaseUrl.TrimEnd('/') + "/" + dest.TrimStart('/')
                );
                request.Headers.Range = new RangeHeaderValue(chunk.Start, chunk.End);
                using var response = await HttpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead
                    )
                    .ConfigureAwait(false); // 非UI上下文切换

                response.EnsureSuccessStatusCode();
                var stream = await response
                    .Content.ReadAsStreamAsync()
                    .ConfigureAwait(false);
                if (chunk.Start < 0 || chunk.End < chunk.Start)
                {
                    Console.WriteLine($"分片范围无效，start={chunk.Start},end={chunk.End}");
                    throw new ArgumentException($"分片范围无效: {chunk.Start}-{chunk.End}");
                }

                long totalWritten = 0;
                long chunkTotalSize = chunk.End - chunk.Start + 1;
                var memoryPool = ArrayPool<byte>.Shared;

                fileStream.Seek(chunk.Start, SeekOrigin.Begin);
                bool isBreak = false;
                while (totalWritten < chunkTotalSize)
                {
                    int bytesToRead = (int)Math.Min(MaxBufferSize, chunkTotalSize - totalWritten);
                    byte[] buffer = memoryPool.Rent(bytesToRead);
                    int bytesRead = await stream
                        .ReadAsync(buffer.AsMemory(0, bytesToRead))
                        .ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        isBreak = true;
                    }
                    await fileStream
                        .WriteAsync(buffer.AsMemory(0, bytesRead))
                        .ConfigureAwait(false);
                    totalWritten += bytesRead;
                    accumulatedBytes += bytesRead;
                    
                }
                if (accumulatedBytes > 0 && !isBreak)
                {
                }
                if (totalWritten != chunkTotalSize)
                {
                    throw new IOException($"分片写入不完整: {totalWritten}/{chunkTotalSize}");
                }
                fileStream.SetLength(size);
                await fileStream.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }


    private async Task<bool> VaildateFullFile(string md5Value, string filePath)
    {
        const int bufferSize = 262144; // 80KB缓冲区
        using var md5 = MD5.Create();
        var memoryPool = ArrayPool<byte>.Shared;
        const long UpdateThreshold = 1048576; // 1MB进度更新阈值
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
                    byte[] buffer = memoryPool.Rent(bufferSize);
                    try
                    {
                        int bytesRead = await fs.ReadAsync(
                                buffer.AsMemory(0, bufferSize)
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
            Console.WriteLine(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }



    private async Task DownloadFileByChunks(
        string dest,
        string filePath,
        long start,
        long end,
        bool isLast = false,
        long allSize = 0L
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
            long accumulatedBytes = 0;
            if (start == 0 && end == -1)
            {
                Console.WriteLine($"文件{filePath}，分片数据错误，start={start},end={end}");
            }
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                BaseUrl.TrimEnd('/') + "/" + dest.TrimStart('/')
            );
            request.Headers.Range = new RangeHeaderValue(start, end);
            using var response = await HttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead
            );
            var stream = await response.Content.ReadAsStreamAsync();
            if (start < 0 || end < start)
            {
                Console.WriteLine($"分片范围无效: {start}-{end}");
                throw new ArgumentException($"分片范围无效: {start}-{end}");
            }

            long totalWritten = 0;
            long chunkTotalSize = end - start + 1;
            var memoryPool = ArrayPool<byte>.Shared;
            fileStream.Seek(start, SeekOrigin.Begin);
            bool isBreak = false;
            while (totalWritten < chunkTotalSize)
            {
                int bytesToRead = (int)Math.Min(MaxBufferSize, chunkTotalSize - totalWritten);
                byte[] buffer = ArrayPool<byte>.Shared.Rent(bytesToRead);
                try
                {
                    int bytesRead = await stream
                        .ReadAsync(buffer.AsMemory(0, bytesToRead))
                        .ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        isBreak = true;
                        break;
                    }
                    await fileStream
                        .WriteAsync(buffer.AsMemory(0, bytesRead))
                        .ConfigureAwait(false);
                    totalWritten += bytesRead;
                    accumulatedBytes += bytesRead;
                    if (accumulatedBytes >= UpdateThreshold)
                    {
                        accumulatedBytes = 0;
                    }

                }
                catch (Exception ex)
                {

                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            if (accumulatedBytes > 0 && !isBreak)
            {
            }
            if (isLast)
                fileStream.SetLength(allSize);
            await fileStream.FlushAsync();
            stream.Close();
            await stream.DisposeAsync();
        }
    }

    private async Task<bool> ValidateFileChunks(IndexChunkInfo file, string filePath)
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
                long offset = file.Start;
                long remaining = file.End - file.Start + 1;
                bool isValid = true;
                fs.Seek(offset, SeekOrigin.Begin);
                using (var md5 = MD5.Create())
                {
                    long accumulatedBytes = 0L;
                    while (remaining > 0 && isValid)
                    {
                        var buffer = memoryPool.Rent(MaxBufferSize);
                        try
                        {
                            int bytesRead = await fs.ReadAsync(
                                    buffer,
                                    0,
                                    MaxBufferSize
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
                               
                                accumulatedBytes = 0;
                            }
                        }
                        catch (IOException ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                        finally
                        {
                            memoryPool.Return(buffer);
                        }
                    }
                    if (accumulatedBytes > 0 && accumulatedBytes < UpdateThreshold)
                    {
                        
                    }
                    md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    string hash = BitConverter.ToString(md5.Hash!).Replace("-", "").ToLower();
                    isValid = hash == file.Md5.ToLower();
                    Console.WriteLine($"分片校验结果{hash}|{file.Md5}");
                    return !isValid;
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException();
            }
        }
    }
}
