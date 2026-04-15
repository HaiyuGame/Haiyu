using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Text;
using Haiyu.Common;
using Serilog.Core;
using Waves.Api.Models;
using Waves.Api.Models.Launcher;
using Waves.Core.Common;
using Waves.Core.Common.Downloads;
using Waves.Core.Contracts;
using Waves.Core.Contracts.Events;
using Waves.Core.Helpers;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Waves.Core.GameContext.KruoGameContextBaseV2.Common;

/// <summary>
/// 进行修复或下载使用的工具类，其中使用IProgress进行回调，再由核心回调事件结束
/// </summary>
public sealed class DownloadAndVerifyResource : IProgressSetup, IAsyncDisposable
{
    #region Param
    private List<IndexResource> _resource;
    private bool isDelete;
    private string _folder;
    private string _baseUrl;
    private bool _isProd;
    private IHttpClientService _httpClientService;
    private GameLauncherSource? _launcher;
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
    #endregion

    private DownloadState _downloadState;

    public Dictionary<string, object> Param { get; private set; }
    public IGameEventPublisher GameEventPublisher { get; private set; }
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

    public void SetParam(Dictionary<string, object> param, IGameEventPublisher gameEventPublisher)
    {
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
        if (!Param.CheckParam<IEnumerable<IndexResource>>("resource", out var resources))
        {
            return false;
        }
        if (!Param.CheckParam<GameLauncherSource>("launcher", out var launcher))
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
        if (!Param.CheckParam<string>("baseUrl", out var baseUrl))
        {
            return false;
        }
        if(!Param.CheckParam<bool>("isProd",out var isProd))
        {
            return false;
        }
        this._resource = resources?.ToList()!;
        this.isDelete = isDelete!;
        this._folder = folder!;
        this._httpClientService = httpService!;
        this._launcher = launcher;
        this._downloadState = downloadState!;
        this._baseUrl = baseUrl!;
        this._isProd = isProd;
        InitProgress();
        return true;
    }

    public async Task<object?> ExecuteAsync()
    {
        try
        {
            if (isDelete)
            {
                Logger.WriteInfo("修复游戏，开始删除本地多余文件");
                var localFile = new DirectoryInfo(_folder).GetFiles(
                    "*",
                    SearchOption.AllDirectories
                );
                var serverFileSet = new HashSet<string>(
                    _resource.Select(x => BuildFileHelper.BuildFilePath(_folder, x).ToLower())
                );

                var filesToDelete = localFile
                    .Where(f =>
                    {
                        return !serverFileSet.Contains(f.FullName.ToLower());
                    })
                    .ToList();

                if (filesToDelete.Any())
                {
                    foreach (var file in filesToDelete)
                    {
                        File.Delete(file.FullName);
                    }
                    var fileNames = filesToDelete.Select(f => Path.GetFileName(f.FullName));
                    Logger.WriteInfo($"删除：删除版本旧文件{string.Join(',', fileNames)}");
                }
            }
            ParallelOptions options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = _downloadState.CancelToken.Token,
            };
            _downloadState.IsActive = true;
            await ParallelDownloadAsync(
                    _downloadState,
                    _resource,
                    _launcher!.ResourceDefault.CdnList,
                    options,
                    _folder
                )
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<bool> ParallelDownloadAsync(
        DownloadState downloadState,
        List<IndexResource> resource,
        List<CdnList> cdns,
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
                                if (_disposed || _downloadState.CancelToken.IsCancellationRequested)
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
                                this.ProgressValue = (double)args.CurrentSize / (double)args.TotalSize;
                                this.GameEventPublisher.Publish(args);
                            }
                        );
                    var filePath = BuildFileHelper.BuildFilePath(folder, item);
                    var downloadUrl = Path.Combine(this._baseUrl, item.Dest).Replace("\\","/");
                    if (File.Exists(filePath))
                    {
                        if (item.ChunkInfos == null)
                        {
                            var checkResult = await VerifyTask.VaildateFullFile(
                                item.Md5,
                                filePath,
                                downloadState,
                                _downloadState.CancelToken,
                                progress: progress
                            );
                            if (checkResult)
                            {
                                Logger.WriteInfo("需要全量下载……");
                                await DownloadTask.DownloadFileByFull(
                                    this._httpClientService,
                                    downloadUrl,
                                    item.Size,
                                    filePath,
                                    new()
                                    {
                                        Start = 0,
                                        End = item.Size - 1,
                                        Md5 = item.Md5,
                                    },
                                    downloadState,
                                    _downloadState.CancelToken,
                                    progress: progress
                                );
                            }
                            else
                            {
                                if (!_disposed && !_downloadState.CancelToken.IsCancellationRequested)
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
                            var fileName = System.IO.Path.GetFileName(filePath);
                            for (int i = 0; i < item.ChunkInfos.Count; i++)
                            {
                                var needDownload = await VerifyTask.ValidateFileChunks(
                                    item.ChunkInfos[i],
                                    filePath,
                                    downloadState,
                                    _downloadState.CancelToken,
                                    progress: progress
                                );
                                if (needDownload)
                                {
                                    Logger.WriteInfo($"分片[{i}]需要全量下载……");
                                    if (i == item.ChunkInfos.Count - 1)
                                    {
                                        await DownloadTask.DownloadFileByChunks(
                                            httpClientService: this._httpClientService,
                                            downloadUrl,
                                            filePath,
                                            item.ChunkInfos[i].Start,
                                            item.ChunkInfos[i].End,
                                            true,
                                            item.Size,
                                            downloadState,
                                            _downloadState.CancelToken,
                                            progress: progress
                                        );
                                    }
                                    else
                                    {
                                        await DownloadTask.DownloadFileByChunks(
                                            httpClientService: this._httpClientService,
                                            downloadUrl,
                                            filePath,
                                            item.ChunkInfos[i].Start,
                                            item.ChunkInfos[i].End,
                                            false,
                                            downloadCts: _downloadState.CancelToken,
                                            state: downloadState,
                                            progress: progress
                                        );
                                    }
                                }
                                else
                                {
                                    if (!_disposed && !_downloadState.CancelToken.IsCancellationRequested)
                                    {
                                        var args = UpdateFileProgress(
                                            GameContextActionType.Verify,
                                            item.ChunkInfos[i].End - item.ChunkInfos[i].Start,
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
                        Logger.WriteInfo($"文件不存在，全量下载");
                        await DownloadTask.DownloadFileByFull(
                            httpClientService: this._httpClientService,
                            downloadUrl,
                            item.Size,
                            filePath,
                            new IndexChunkInfo()
                            {
                                Start = 0,
                                End = item.Size - 1,
                                Md5 = item.Md5,
                            },
                            downloadState,
                            _downloadState.CancelToken,
                            progress: progress
                        );
                    }
                }
            );
            return true;
        }
        catch (Exception ex)
        {
            Logger.WriteError($"校验失败！{ex}");
            return false;
        }
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
        catch (Exception)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await CancelAsync();
        this._resource.Clear();
        this._launcher = null;
    }
}
