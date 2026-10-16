using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Waves.Core.GameContext.KruoGameContextBaseV2.Common;

/// <summary>
/// 进行修复或下载使用的工具类，其中使用IProgress进行回调，再由核心回调事件结束
/// </summary>
public sealed class DownloadAndVerifyResource : IProgressSetup, IAsyncDisposable
{
    #region Param
    private List<GameFileInfo> _resource;
    private bool isDelete;
    private string _folder;
    private bool _isProd;
    private List<string>? skipVerifyFile;
    private bool fastVerify = false;
    private IHttpClientService _httpClientService;
    private long _totalDownloadedBytes;
    private long _totalProgressSize;
    private long _totalProgressTotal;
    private long _totalVerifiedBytes;
    private double _downloadSpeed;
    private double _verifySpeed;
    private long _totalfileSize;
    private int _totalFileTotal;
    private volatile bool _disposed;
    private long _generation;
    private readonly object _progressGate = new();
    private long _speedSampleTimestamp;
    private double _smoothedDownloadSpeed;
    private double _smoothedVerifySpeed;
    #endregion

    private DownloadState _downloadState;

    public Dictionary<string, object> Param { get; private set; }
    public IGameEventPublisher<GameContextOutputArgs> GameEventPublisher { get; private set; }
    public LoggerService Logger { get; }
    public string ProgressName { get; set; }
    public double ProgressValue { get; set; }

    public bool CanPause => true;

    public bool CanStop => true;

    /// <summary>
    /// 构造传参
    /// </summary>
    /// <param name="param"></param>
    public DownloadAndVerifyResource(LoggerService loggerService)
    {
        Logger = loggerService;
    }

    public void SetParam(
        Dictionary<string, object> param,
        IGameEventPublisher<GameContextOutputArgs> gameEventPublisher
    )
    {
        _generation = GameContextOutputArgs.CurrentGeneration.Value;
        Param = param;
        this.GameEventPublisher = gameEventPublisher;
    }

    /// <summary>
    /// 开始执行
    /// </summary>
    /// <param name="isSync">是否同步执行</param>
    /// <returns></returns>
    public async Task<object?> ExecuteAsync(bool isSync = false)
    {
        if (!(await CheckAsync()))
        {
            return null;
        }
        if (isSync)
        {
            return await ExecuteAsync().ConfigureAwait(false);
        }
        else
        {
            Task.Run(async () => await ExecuteAsync()).ConfigureAwait(false);
            return true;
        }
    }

    public void InitProgress()
    {
        _totalfileSize = this._resource.Sum(x => x.Size);
        _totalFileTotal = _resource.Count;
        _totalProgressSize = 0L;
        _totalProgressTotal = 0L;
        _totalVerifiedBytes = 0;
        _totalDownloadedBytes = 0;
        _downloadSpeed = _verifySpeed = 0;
        _smoothedDownloadSpeed = _smoothedVerifySpeed = 0;
        _speedSampleTimestamp = Stopwatch.GetTimestamp();
    }

    public async Task<bool> CheckAsync()
    {
        if (!Param.CheckParam<IEnumerable<GameFileInfo>>("resource", out var resources))
        {
            return false;
        }
        if (!Param.CheckParam<bool>("isDelete", out var isDelete))
        {
            return false;
        }
        if (!Param.CheckParam<string>("folder", out var folder))
        {
            return false;
        }
        if (!Param.CheckParam<IHttpClientService>("httpClient", out var httpService))
        {
            return false;
        }
        if (!Param.CheckParam<DownloadState>("downloadState", out var downloadState))
        {
            return false;
        }
        if (!Param.CheckParam<bool>("isProd", out var isProd))
        {
            return false;
        }
        Param.CheckParam<bool>("fastVerify", out var firstVerify);
        Param.CheckParam<List<string>>("skipVerifyFile", out var skipVerifyFile);
        this._resource = resources?.ToList()!;
        this.isDelete = isDelete!;
        this._folder = folder!;
        this._httpClientService = httpService!;
        this._downloadState = downloadState!;
        this._isProd = isProd;
        this.skipVerifyFile = skipVerifyFile;
        this.fastVerify = firstVerify;
        InitProgress();
        return true;
    }

    public async Task<object?> ExecuteAsync()
    {
        try
        {
            ParallelOptions options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = _downloadState.CancelToken.Token,
            };
            _downloadState.IsActive = true;
            var downloadSucceeded = await ParallelDownloadAsync(
                    _downloadState,
                    _resource,
                    options,
                    _folder
                )
                .ConfigureAwait(false);

            // 只有在校验/下载全部成功且任务未取消时，才删除多余文件。
            // 避免用户取消或修复失败后，已删除的游戏配置无法恢复。
            if (!downloadSucceeded || _downloadState.CancelToken.IsCancellationRequested)
                return false;

            if (isDelete)
                DeleteExtraFiles();

            return true;
        }
        catch (Exception)
        {
            throw;
        }
    }

    private void DeleteExtraFiles()
    {
        Logger.WriteInfo("修复游戏，开始删除本地多余文件");
        var localFile = new DirectoryInfo(_folder).GetFiles("*", SearchOption.AllDirectories);
        var serverFileSet = new HashSet<string>(
            _resource.Select(x => BuildFileHelper.BuildFilePath(_folder, x).ToLower())
        );

        var filesToDelete = localFile
            .Where(f => !serverFileSet.Contains(f.FullName.ToLower()))
            .ToList();

        if (!filesToDelete.Any())
            return;

        foreach (var file in filesToDelete)
            TryDeleteFile(file.FullName);
        var fileNames = filesToDelete.Select(f => Path.GetFileName(f.FullName));
        Logger.WriteInfo($"删除：删除版本旧文件{string.Join(',', fileNames)}");
    }

    private void TryDeleteFile(string fileName)
    {
        try
        {
            if (this.skipVerifyFile != null && this.skipVerifyFile.Contains(fileName))
                return;
            File.Delete(fileName);
        }
        catch (Exception ex)
        {
            Logger.WriteError(ex.Message + ex.StackTrace);
        }
    }

    public async Task<bool> ParallelDownloadAsync(
        DownloadState downloadState,
        List<GameFileInfo> resource,
        ParallelOptions options,
        string folder
    )
    {
        try
        {
            await GameEventPublisher.PublisAsync(
                GameContextActionType.CdnSelect,
                this.ProgressName,
                _isProd
            );
            await Parallel.ForEachAsync(
                resource,
                options,
                async (item, token) =>
                {
                    if (_downloadState.CancelToken.Token.IsCancellationRequested)
                    {
                        if (downloadState != null)
                            await GameEventPublisher.PublisAsync(
                                GameContextActionType.None,
                                "取消下载"
                            );

                        return;
                    }
                    IProgress<(GameContextActionType, bool, long, string, long, long)> progress =
                        new TransferProgress(
                            value =>
                            {
                                if (
                                    _disposed
                                    || _downloadState.CancelToken.IsCancellationRequested
                                    || !(_downloadState?.IsActive ?? false)
                                )
                                    return;
                                var args = UpdateFileProgress(
                                    value.Item1,
                                    value.Item3,
                                    // 真实读写字节只计速率；验证成功后再确认完成量，重试不重复累加。
                                    false,
                                    filePath: value.Item4,
                                    currentFileSize: value.Item5,
                                    fileMaxSize: value.Item6
                                );
                                args.Prod = this._isProd;
                                this.ProgressValue =
                                    (double)args.CurrentSize / (double)args.TotalSize;
                                this.GameEventPublisher.Publish(args);
                            }
                        );
                    var filePath = BuildFileHelper.BuildFilePath(folder, item);
                    if (this.skipVerifyFile != null && this.skipVerifyFile.Contains(filePath))
                    {
                        if (
                            !_disposed
                            && !_downloadState.CancelToken.IsCancellationRequested
                            && (_downloadState?.IsActive ?? false)
                        )
                        {
                            var args = UpdateFileProgress(
                                GameContextActionType.Verify,
                                item.Size,
                                true
                            );
                            GameEventPublisher.Publish(args);
                        }
                        return;
                    }
                    if (item.Size == 0 && !File.Exists(filePath)) await File.WriteAllBytesAsync(filePath, [], token);
                    var useChunks = HasCompleteChunkCoverage(item);
                    if (item.Chunks.Count > 0 && !useChunks && string.IsNullOrWhiteSpace(item.Hash))
                        throw new InvalidDataException($"分片未完整覆盖文件且缺少整文件哈希：{item.Dest}");
                    if (File.Exists(filePath))
                    {
                        if (!useChunks)
                        {
                            var checkResult = !BuildFileHelper.GetFileLength(filePath, out var size) || size != item.Size
                                || (!string.IsNullOrWhiteSpace(item.Hash) && await VerifyTask.ValidateGameFileAsync(
                                    item.Hash, filePath, downloadState, _downloadState.CancelToken, progress));
                            if (checkResult)
                            {
                                Logger.WriteInfo($"需要全量下载……{item.Dest}");
                                await WithCdnFallbackAsync(item, url => DownloadTask.DownloadGameFileByFull(
                                    this._httpClientService,
                                    url,
                                    item.Size,
                                    filePath,
                                    new()
                                    {
                                        Start = 0,
                                        End = item.Size - 1,
                                        Hash = item.Hash,
                                    },
                                    downloadState,
                                    _downloadState.CancelToken,
                                    progress: progress
                                ), () => NeedsDownloadAsync(item, filePath, progress));
                                PublishCompletedBytes(item.Size, filePath, item.Size);
                            }
                            else
                            {
                                if (
                                    !_disposed
                                    && !_downloadState.CancelToken.IsCancellationRequested
                                    && (_downloadState?.IsActive ?? false)
                                )
                                {
                                    var args = UpdateFileProgress(
                                        GameContextActionType.Verify,
                                        item.Size,
                                        true
                                    );
                                    GameEventPublisher.Publish(args);
                                }
                            }
                        }
                        else
                        {
                            if (
                                fastVerify
                                && BuildFileHelper.GetFileLength(filePath, out var currentFileSize)
                                && currentFileSize == item.Size
                            )
                            {
                                var lastChunk = item.Chunks.Last();
                                var firstChunk = item.Chunks.First();
                                var splitChunk = item.Chunks[item.Chunks.Count / 2];
                                var needDownload = (
                                    await VerifyTask.ValidateFileChunks(
                                        lastChunk,
                                        filePath,
                                        downloadState,
                                        _downloadState.CancelToken
                                    )
                                    || await VerifyTask.ValidateFileChunks(
                                        firstChunk,
                                        filePath,
                                        downloadState,
                                        _downloadState.CancelToken
                                    )
                                    || await VerifyTask.ValidateFileChunks(
                                        splitChunk,
                                        filePath,
                                        downloadState,
                                        _downloadState.CancelToken
                                    )
                                );
                                //快速校验，跳过其他分片，只校验文件大小和尾部hash是否对齐
                                if (!needDownload)
                                {
                                    if (
                                        !_disposed
                                        && !_downloadState.CancelToken.IsCancellationRequested
                                        && (_downloadState?.IsActive ?? false)
                                    )
                                    {
                                        var args = UpdateFileProgress(
                                            GameContextActionType.Verify,
                                            item.Size,
                                            true
                                        );

                                        GameEventPublisher.Publish(args);
                                    }
                                    return;
                                }
                            }
                            var fileName = System.IO.Path.GetFileName(filePath);
                            for (int i = 0; i < item.Chunks.Count; i++)
                            {
                                var needDownload = await VerifyTask.ValidateFileChunks(
                                    item.Chunks[i],
                                    filePath,
                                    downloadState,
                                    _downloadState.CancelToken,
                                    progress: progress
                                );
                                if (needDownload)
                                {
                                    Logger.WriteInfo($"分片[{i}]需要全量下载……{item.Dest}");
                                    if (i == item.Chunks.Count - 1)
                                    {
                                        await WithCdnFallbackAsync(item, url => DownloadTask.DownloadFileByChunks(
                                            httpClientService: this._httpClientService,
                                            url,
                                            filePath,
                                            item.Chunks[i].Start,
                                            item.Chunks[i].End,
                                            true,
                                            item.Size,
                                            downloadState,
                                            _downloadState.CancelToken,
                                            progress: progress
                                        ), () => VerifyTask.ValidateFileChunks(item.Chunks[i], filePath, _downloadState, _downloadState.CancelToken));
                                    }
                                    else
                                    {
                                        await WithCdnFallbackAsync(item, url => DownloadTask.DownloadFileByChunks(
                                            httpClientService: this._httpClientService,
                                            url,
                                            filePath,
                                            item.Chunks[i].Start,
                                            item.Chunks[i].End,
                                            false,
                                            downloadCts: _downloadState.CancelToken,
                                            state: downloadState,
                                            progress: progress
                                        ), () => VerifyTask.ValidateFileChunks(item.Chunks[i], filePath, _downloadState, _downloadState.CancelToken));
                                    }
                                    PublishCompletedBytes(item.Chunks[i].End - item.Chunks[i].Start + 1,
                                        filePath, item.Size);
                                }
                                else
                                {
                                    if (
                                        !_disposed
                                        && !_downloadState.CancelToken.IsCancellationRequested
                                        && (_downloadState?.IsActive ?? false)
                                    )
                                    {
                                        var args = UpdateFileProgress(
                                            GameContextActionType.Verify,
                                            item.Chunks[i].End - item.Chunks[i].Start + 1,
                                            true
                                        );
                                        GameEventPublisher.Publish(args);
                                    }
                                }
                            }
                            if (BuildFileHelper.GetFileLength(filePath, out var existingSize) && existingSize > item.Size)
                            {
                                using var truncate = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None);
                                truncate.SetLength(item.Size);
                            }
                        }
                    }
                    else
                    {
                        Logger.WriteInfo($"文件不存在，全量下载{item.Dest}");
                        await WithCdnFallbackAsync(item, url => DownloadTask.DownloadGameFileByFull(
                            httpClientService: this._httpClientService,
                            url,
                            item.Size,
                            filePath,
                            new GameFileChunkInfo()
                            {
                                Start = 0,
                                End = item.Size - 1,
                                Hash = item.Hash,
                            },
                            downloadState,
                            _downloadState.CancelToken,
                            progress: progress
                        ), () => NeedsDownloadAsync(item, filePath, progress));
                        PublishCompletedBytes(item.Size, filePath, item.Size);
                    }
                    // 完整覆盖的分片已经逐片校验（修复的分片也单独复验），不再扫描整文件。
                    if (!BuildFileHelper.GetFileLength(filePath, out var finalSize) || finalSize != item.Size)
                        throw new InvalidDataException($"文件大小校验失败：{item.Dest}");
                }
            );
            return true;
        }
        catch (OperationCanceledException) when (_downloadState.CancelToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.WriteError($"校验失败！{ex}");
            GameEventPublisher.Publish(
                new GameContextOutputArgs
                {
                    Type = GameContextActionType.TipMessage,
                    TipMessage = $"校验失败！{ex.Message}",
                }
            );
            return false;
        }
    }

    // Inline reporting avoids a ThreadPool callback for every disk-read notification and
    // prevents queued old progress from overwriting a later stage/completion event.
    private sealed class TransferProgress(Action<(GameContextActionType, bool, long, string, long, long)> report)
        : IProgress<(GameContextActionType, bool, long, string, long, long)>
    {
        public void Report((GameContextActionType, bool, long, string, long, long) value) => report(value);
    }

    private void PublishCompletedBytes(long size, string filePath, long fileTotal)
    {
        if (_disposed || _downloadState.CancelToken.IsCancellationRequested || !_downloadState.IsActive)
            return;
        GameEventPublisher.Publish(UpdateFileProgress(GameContextActionType.Verify, size, true,
            filePath: filePath, currentFileSize: fileTotal, fileMaxSize: fileTotal));
    }

    private static bool HasCompleteChunkCoverage(GameFileInfo item)
    {
        if (item.Chunks.Count == 0 || item.Size <= 0) return false;
        long next = 0;
        foreach (var chunk in item.Chunks.OrderBy(x => x.Start))
        {
            if (chunk.Start != next || chunk.End < chunk.Start || chunk.End >= item.Size
                || string.IsNullOrWhiteSpace(chunk.Hash)) return false;
            next = chunk.End + 1;
        }
        return next == item.Size;
    }

    private async Task<bool> NeedsDownloadAsync(GameFileInfo item, string path,
        IProgress<(GameContextActionType, bool, long, string, long, long)>? progress = null)
    {
        if (!BuildFileHelper.GetFileLength(path, out var size) || size != item.Size) return true;
        if (!string.IsNullOrWhiteSpace(item.Hash))
            return await VerifyTask.ValidateGameFileAsync(item.Hash, path, _downloadState, _downloadState.CancelToken, progress);
        foreach (var chunk in item.Chunks)
            if (await VerifyTask.ValidateFileChunks(chunk, path, _downloadState, _downloadState.CancelToken, progress)) return true;
        return false;
    }

    private async Task WithCdnFallbackAsync(GameFileInfo item, Func<string, Task> action, Func<Task<bool>> verify)
    {
        Exception? lastError = null;
        var urls = new[] { item.Url }.Concat(item.UrlCandidates).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct();
        foreach (var url in urls)
        {
            _downloadState.CancelToken.Token.ThrowIfCancellationRequested();
            try
            {
                await action(url);
                if (await verify()) throw new InvalidDataException($"下载内容校验失败：{item.Dest}");
                return;
            }
            catch (OperationCanceledException) when (_downloadState.CancelToken.IsCancellationRequested) { throw; }
            catch (Exception ex) { lastError = ex; }
        }
        throw new IOException($"下载失败：{item.Dest}", lastError);
    }

    internal GameContextOutputArgs UpdateFileProgress(
        GameContextActionType type,
        long fileSize,
        bool isAdd = true,
        string tip = "",
        string filePath = null,
        long currentFileSize = 0,
        long fileMaxSize = 0
    )
    {
        lock (_progressGate)
        {
            if (type == GameContextActionType.Download)
            {
                Interlocked.Add(ref _totalDownloadedBytes, fileSize);
                if (isAdd)
                    Interlocked.Add(ref _totalProgressSize, fileSize);
            }
            else if (type == GameContextActionType.Verify)
            {
                if (!isAdd)
                    Interlocked.Add(ref _totalVerifiedBytes, fileSize);
                if (isAdd)
                    Interlocked.Add(ref _totalProgressSize, fileSize);
            }
            var elapsed = Stopwatch.GetElapsedTime(_speedSampleTimestamp).TotalSeconds;
            if (elapsed >= 1)
            {
                _downloadSpeed = Interlocked.Exchange(ref _totalDownloadedBytes, 0) / elapsed;
                _verifySpeed = Interlocked.Exchange(ref _totalVerifiedBytes, 0) / elapsed;
                _smoothedDownloadSpeed = _downloadSpeed > 0
                    ? (_smoothedDownloadSpeed > 0 ? 0.3 * _downloadSpeed + 0.7 * _smoothedDownloadSpeed : _downloadSpeed) : 0;
                _smoothedVerifySpeed = _verifySpeed > 0
                    ? (_smoothedVerifySpeed > 0 ? 0.3 * _verifySpeed + 0.7 * _smoothedVerifySpeed : _verifySpeed) : 0;
                _speedSampleTimestamp = Stopwatch.GetTimestamp();
            }
            // 下载/校验共用任务进度，但速率独立；校验读取字节不重复累加到完成进度。
            var speed = type == GameContextActionType.Download ? _smoothedDownloadSpeed : _smoothedVerifySpeed;
            var remainingBytes = Math.Max(0, _totalfileSize - _totalProgressSize);
            TimeSpan? remainingTime = null;
            if (!_downloadState.IsPaused && !_downloadState.CancelToken.IsCancellationRequested && _totalfileSize > 0)
            {
                if (remainingBytes == 0) remainingTime = TimeSpan.Zero;
                else if (speed > 0)
                {
                    var seconds = remainingBytes / speed;
                    if (double.IsFinite(seconds) && seconds < TimeSpan.MaxValue.TotalSeconds)
                        remainingTime = TimeSpan.FromSeconds(Math.Ceiling(seconds));
                }
            }
            var args = new GameContextOutputArgs
            {
                Generation = _generation,
                RemainingTime = remainingTime,
                Type = type,
                CurrentSize = _totalProgressSize,
                TotalSize = _totalfileSize,
                FileTotal = _totalFileTotal,
                DownloadSpeed = _downloadSpeed,
                FilePath = filePath,
                FileCurrentSize = currentFileSize,
                FileTotalSize = fileMaxSize,
                Prod = _isProd,
                IsCancel = this._downloadState.CancelToken.IsCancellationRequested,
                VerifySpeed = _verifySpeed,
                IsAction = this._downloadState?.IsActive ?? false,
                IsPause = _downloadState?.IsPaused ?? false,
                TipMessage = tip,
            };
            return args;
        }
    }

    public async Task<bool> CancelAsync()
    {
        try
        {
            await this._downloadState.CancelToken.CancelAsync();
            return true;
        }
        catch (Exception ex)
        {
            Logger.WriteError($"取消任务失败: {ex.Message}");
            GameEventPublisher.Publish(
                new GameContextOutputArgs
                {
                    Type = GameContextActionType.TipMessage,
                    TipMessage = $"取消任务失败: {ex.Message}",
                }
            );
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await CancelAsync();
        this._resource.Clear();
    }
}
