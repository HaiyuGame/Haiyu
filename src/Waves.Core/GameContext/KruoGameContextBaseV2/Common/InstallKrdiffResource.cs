using System;
using System.Collections.Generic;
using System.Text;
using Serilog.Core;
using Waves.Core.Common;
using Waves.Core.Common.Downloads;
using Waves.Core.Contracts.Events;
using Waves.Core.Helpers;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Waves.Core.GameContext.KruoGameContextBaseV2.Common;

/// <summary>
/// 安装库洛补丁包资源类
/// </summary>
public class InstallKrdiffResource:IProgressSetup,IAsyncDisposable
{
    private List<IndexResource> krdiffs;
    private string diffFolderPath;
    private string gameBaseFolder;

    public InstallKrdiffResource(
        LoggerService loggerService
    )
    {
        this.Logger = loggerService;
    }

    public Dictionary<string, object> Param { get; private set; }
    public LoggerService Logger { get; }
    public IGameEventPublisher GameEventPublisher { get; private set; }
    public string ProgressName { get; set; }
    public double ProgressValue { get; set; }

    public bool CanPause => false;

    public bool CanStop => false;

    public void SetParam(Dictionary<string, object> param)
    {
        this.Param = param;
    }

    public async Task<bool> CheckAsync()
    {
        //补丁
        if (!Param.CheckParam<List<IndexResource>>("krdiffs", out var krdiffs))
        {
            return false;
        }
        //资源基础路径
        if (!Param.CheckParam<string>("diffFolderPath", out var diffFolderPath))
        {
            return false;
        }
        if(!Param.CheckParam<string>("gameBaseFolder",out var gameBaseFolder))
        this.krdiffs = krdiffs!;
        this.diffFolderPath = diffFolderPath!;
        this.gameBaseFolder = gameBaseFolder!;
        return true;
    }

    public async Task<bool> RunAsync()
    {
        for (int i = 0; i < krdiffs.Count; i++)
        {
            //diffFolderPath 路径为下载补丁包路径
            var krdiffPath = BuildFileHelper.BuildFilePath(diffFolderPath, krdiffs[i]);
            IProgress<(GameContextActionType, string, KrDiffDecompressResult)> progress =
                new Progress<(GameContextActionType, string, KrDiffDecompressResult)>(
                    (s) =>
                    {
                        GameEventPublisher.Publish(
                            new GameContextOutputArgs
                            {
                                Type = GameContextActionType.Decompress,
                                CurrentSize = (long)s.Item3.PatchedCurrentBytes,
                                TotalSize = (long)s.Item3.PatchTotalBytes,
                                DownloadSpeed = 0,
                                VerifySpeed = 0,
                                IsAction = true,
                                IsPause = false,
                                TipMessage = "正在解压合并资源",
                                CurrentDecompressCount = i,
                                MaxDecompressValue = krdiffs.Count,
                            }
                        );
                        ProgressValue = s.Item3.TotalBytesProgress;
                    }
                );
            await DiffDecompressTask.DecompressKrdiffFile(
                gameBaseFolder,
                krdiffPath,
                i,
                krdiffs.Count,
                progress: progress
            );
        }
        return true;
    }

    public void SetParam(Dictionary<string, object> param, IGameEventPublisher gameEventPublisher)
    {
        this.Param = param;
        this.GameEventPublisher = GameEventPublisher;
    }

    /// <summary>
    /// 当前解压任务不可取消，UI界面需要按钮禁用
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task<object?> ExecuteAsync(bool isSync = false)
    {
        if (isSync)
        {
            return await RunAsync();
        }
        else
        {
            Task.Run(async() => await RunAsync());
            return null;
        }
    }
}
