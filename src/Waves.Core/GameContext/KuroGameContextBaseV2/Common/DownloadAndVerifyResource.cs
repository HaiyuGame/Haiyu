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
    private long _lastSpeedBytes;
    private DateTime _lastSpeedUpdateTime;
    private double _downloadSpeed;
    private double _verifySpeed;
    private long _totalfileSize;
    private int _totalFileTotal;
    private volatile bool _disposed;
    private long _generation;
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
        _totalFileTotal = _resource.Count - 1;
        _totalProgressSize = 0L;
        _totalProgressTotal = 0L;
        _totalVerifiedBytes = 0;
        _totalDownloadedBytes = 0;
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
                        new Progress<(GameContextActionType, bool, long, string, long, long)>(
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
                                    value.Item2,
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
                    var downloadUrl = item.Url;
                    if (File.Exists(filePath))
                    {
                        if (item.Chunks.Count == 0)
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
                                ), () => NeedsDownloadAsync(item, filePath));
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
                                            item.Chunks[i].End - item.Chunks[i].Start,
                                            true
                                        );
                                        GameEventPublisher.Publish(args);
                                    }
                                }
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
                        ), () => NeedsDownloadAsync(item, filePath));
                    }
                    if (await NeedsDownloadAsync(item, filePath))
                        throw new InvalidDataException($"文件校验失败：{item.Dest}");
                }
            );
            return true;
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

    private async Task<bool> NeedsDownloadAsync(GameFileInfo item, string path)
    {
        if (!BuildFileHelper.GetFileLength(path, out var size) || size != item.Size) return true;
        if (!string.IsNullOrWhiteSpace(item.Hash))
            return await VerifyTask.ValidateGameFileAsync(item.Hash, path, _downloadState, _downloadState.CancelToken);
        foreach (var chunk in item.Chunks)
            if (await VerifyTask.ValidateFileChunks(chunk, path, _downloadState, _downloadState.CancelToken)) return true;
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

    private GameContextOutputArgs UpdateFileProgress(
        GameContextActionType type,
        long fileSize,
        bool isAdd = true,
        string tip = "",
        string filePath = null,
        long currentFileSize = 0,
        long fileMaxSize = 0
    )
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
        var elapsed = (DateTime.Now - _lastSpeedUpdateTime).TotalSeconds;
        if (elapsed >= 1)
        {
            _downloadSpeed = _totalDownloadedBytes / elapsed;
            _verifySpeed = _totalVerifiedBytes / elapsed;
            Interlocked.Exchange(ref _totalDownloadedBytes, 0);
            Interlocked.Exchange(ref _totalVerifiedBytes, 0);
            var currentBytes = Interlocked.Read(ref _totalDownloadedBytes);
            _lastSpeedBytes = currentBytes;
            _lastSpeedUpdateTime = DateTime.Now;
        }
        var args = new GameContextOutputArgs
        {
            Generation = _generation,
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
