using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Text;
using Haiyu.Common;
using Serilog.Core;
using Waves.Api.Models;
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
public sealed class DownloadAndVerifyResource : IAsyncDisposable
{
    #region Param
    private List<IndexResource> _resource;
    private bool isDelete;
    private CancellationTokenSource cts;
    private string _folder;
    private string _baseUrl;
    private IHttpClientService _httpClientService;
    private GameLauncherSource? _launcher;
    private string _downloadBaseUrl;
    #endregion

    public DownloadState DownloadState { get; set; }

    public CDNSpeedTester CDNSpeedTester { get; }
    public Dictionary<string, object> Param { get; private set; }
    public IGameEventPublisher GameEventPublisher { get; private set; }
    public LoggerService Logger { get; }

    /// <summary>
    /// 构造传参
    /// </summary>
    /// <param name="param"></param>
    public DownloadAndVerifyResource(LoggerService loggerService)
    {
        Logger = loggerService;
        CDNSpeedTester = new();
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
    public async Task<bool> RunAsync(bool isSync = false)
    {
        if (!(await CheckAsync()))
        {
            return false;
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

    public async Task<bool> CheckAsync()
    {
        if (!Param.CheckParam<List<IndexResource>>("resource", out var resources))
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
        this._resource = resources!;
        this.isDelete = isDelete!;
        this.cts = new CancellationTokenSource();
        this._folder = folder!;
        this._httpClientService = httpService!;
        this._launcher = launcher;
        return true;
    }

    public async Task<bool> ExecuteAsync()
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
                CancellationToken = cts.Token,
            };
            await ParallelDownloadAsync(
                    DownloadState,
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
                "正在选择最优CDN"
            );
            const long targetTestSize = 50L * 1024 * 1024;
            var item = resource
                .OrderBy(x => Math.Abs((long)x.Size - targetTestSize))
                .FirstOrDefault();
            item ??= resource.OrderBy(x => x.Size).FirstOrDefault();
            var testUrl = this._launcher.ResourceDefault.Config.BaseUrl + item.Dest;
            var best = await CDNSpeedTester.TestAllAsync(
                _launcher.ResourceDefault.CdnList,
                testUrl,
                TimeSpan.FromSeconds(40)
            );
            this._downloadBaseUrl = best.Url + this._launcher.ResourceDefault.Config.BaseUrl;
            await GameEventPublisher.PublisAsync(GameContextActionType.CdnSelect, "CDN选择完毕");
            await Parallel.ForEachAsync(
                resource,
                options,
                async (item, token) =>
                {
                    if (cts.IsCancellationRequested)
                    {
                        if (downloadState != null)
                            await GameEventPublisher.PublisAsync(
                                GameContextActionType.None,
                                "取消下载"
                            );
                        return;
                    }
                    var filePath = BuildFileHelper.BuildFilePath(folder, item);
                    var downloadUrl = this._downloadBaseUrl + item.Dest;
                    if (File.Exists(filePath))
                    {
                        if (item.ChunkInfos == null)
                        {
                            var checkResult = await VerifyTask.VaildateFullFile(
                                item.Md5,
                                filePath,
                                downloadState,
                                cts,
                                eventPublisher: GameEventPublisher
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
                                    cts,eventPublisher:this.GameEventPublisher
                                );
                            }
                            else
                            {
                                //await UpdateFileProgress(
                                //        GameContextActionType.Verify,
                                //        item.Size,
                                //        true,
                                //        ispred
                                //    )
                                //    .ConfigureAwait(false);
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
                                    cts
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
                                            cts,eventPublisher:this.GameEventPublisher
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
                                            downloadCts: cts,
                                            state: downloadState
                                        );
                                    }
                                }
                                else
                                {
                                    //await UpdateFileProgress(
                                    //        GameContextActionType.Verify,
                                    //        item.ChunkInfos[i].End - item.ChunkInfos[i].Start,
                                    //        true,
                                    //        ispred
                                    //    )
                                    //    .ConfigureAwait(false);
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
                            cts,eventPublisher:this.GameEventPublisher
                        );
                        //await FinalValidation(file, filePath);
                    }
                }
            );
            return true;
        }
        catch (Exception ex)
        {
            Logger.WriteError("校验失败！");
            return false;
        }
    }

    public async Task<bool> CancelAsync()
    {
        try
        {
            await this.cts.CancelAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CancelAsync();
        this._resource.Clear();
        this._launcher = null;
    }
}
