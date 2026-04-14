using System;
using System.Collections.Generic;
using System.Text;
using Waves.Api.Models;
using Waves.Api.Models.Launcher;
using Waves.Core.Common;
using Waves.Core.Contracts.Events;
using Waves.Core.Models;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Waves.Core.Contracts;

public interface IGameContextV2
{
    public string GameContextNameKey { get; }
    public IHttpClientService HttpClientService { get; set; }

    public Task InitAsync();
    public string ContextName { get; }
    public string GamerConfigPath { get; internal set; }
    GameLocalConfig GameLocalConfig { get; }

    public IGameEventPublisher GameEventPublisher { get; }
    GameProgressTracker ProgressState { get; }
    public KuroGameApiConfig Config { get; }

    public GameType GameType { get; }
    Task<FileVersion> GetLocalDLSSAsync();
    Task<FileVersion> GetLocalDLSSGenerateAsync();
    Task<FileVersion> GetLocalXeSSGenerateAsync();
    public Type ContextType { get; }

    public DownloadState? DownloadState { get; }

    public DownloadState? ProdDownloadState { get; }
    public TimeSpan GetGameTime();

    public Task<bool> RepairGameAsync();

    #region Launcher
    Task<GameLauncherSource?> GetGameLauncherSourceAsync(
        KuroGameApiConfig apiConfig = null,
        CancellationToken token = default
    );

    Task<GameLauncherStarter?> GetLauncherStarterAsync(CancellationToken token = default);
    #endregion

    #region Core
    Task<GameContextStatus> GetGameContextStatusAsync(CancellationToken token = default);
    #endregion

    #region Downloader
    Task<IndexGameResource?> GetGameResourceAsync(
        ResourceDefault ResourceDefault,
        CancellationToken token = default
    );
    Task<PatchIndexGameResource?> GetPatchGameResourceAsync(
        string url,
        CancellationToken token = default
    );
    Task<GameContextConfig> ReadContextConfigAsync(CancellationToken token = default);

    /// <summary>
    /// 开始下载
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    Task<bool> StartDownloadTaskAsync(
        string folder,
        bool isDelete = false,
        CancellationToken token = default
    );

    /// <summary>
    /// 进行预下载
    /// </summary>
    /// <param name="launcher"></param>
    /// <param name="downloadFolder"></param>
    /// <returns></returns>
    Task<bool> StartProdDownloadGameResourceAsync();

    bool IsDownloadTaskCancel();

    /// <summary>
    /// 恢复任务
    /// </summary>
    /// <returns></returns>
    Task<bool> ResumeDownloadAsync();

    /// <summary>
    /// 取消下载
    /// </summary>
    /// <returns></returns>
    Task<bool> StopCannelTaskAsync();

    /// <summary>
    /// 开始任务
    /// </summary>
    /// <returns></returns>
    Task<bool> PauseDownloadAsync();

    /// <summary>
    /// 设置限速
    /// </summary>
    /// <param name="bytesPerSecond"></param>
    /// <returns></returns>
    Task SetDownloadSpeedAsync(long bytesPerSecond);

    /// <summary>
    /// 重新推送上一次的输出（用于页面重建后恢复显示）
    /// </summary>
    Task ReEmitLastOutputAsync(bool isPred = false);

    /// <summary>
    /// 获得游戏登陆的OAuth的代码
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<List<KRSDKLauncherCache>?> GetLocalGameOAuthAsync(CancellationToken token = default);

    /// <summary>
    /// 安装预下载内容
    /// </summary>
    /// <returns></returns>
    Task StartInstallGameResource(
        GameLauncherSource launcher,
        PatchConfig previous,
        PatchIndexGameResource patch,
        bool isProd = false
    );
    Task<bool> StartGameAsync();
    Task<bool> UpdateGameResourceAsync();
    Task DeleteResourceAsync(
        IProgress<(double deletedCount, double totalCount)> progress
    );

    #endregion

    Task StartInstallGameResource(bool isProd = false);

    Task<LIndex?> GetDefaultLauncherValue(CancellationToken token = default);

    Task<LauncherBackgroundData?> GetLauncherBackgroundDataAsync(
        string backgroundCode,
        CancellationToken token = default
    );

    #region 本地游戏体力查询接口
    Task<QueryPlayerInfo?> QueryPlayerInfoAsync(
        string oAutoCode,
        CancellationToken token = default
    );

    Task<QueryRoleInfo?> QueryRoleInfoAsync(
        string oautoCode,
        string playerId,
        string region,
        CancellationToken token = default
    );
    #endregion
}
