using Waves.Core.Models.Options;

namespace Waves.Core.Contracts;

public interface IGameContextV2
{
    public string GameContextNameKey { get; }
    public IHttpClientService HttpClientService { get; set; }

    public Task InitAsync();
    public string ContextName { get; }
    public string DisplayName { get; }
    public string GamerConfigPath { get; internal set; }
    GameLocalConfig GameLocalConfig { get; }

    public IGameEventPublisher<GameContextOutputArgs> GameEventPublisher { get; }

    public SystemEventPublisher SystemEventPublisher { get; }
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

    public Task<bool> RepairGameAsync(bool isDelete=true,List<string>? skipFilePath = null);

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
        string url,
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
        InstallOption option
    );
    /// <summary>
    /// 开始游戏
    /// </summary>
    /// <returns></returns>
    Task<bool> StartGameAsync();

    /// <summary>
    /// 更新游戏
    /// </summary>
    /// <returns></returns>
    Task<bool> UpdateGameResourceAsync();
    Task DeleteResourceAsync(
        IProgress<(double deletedCount, double totalCount)> progress
    );

    /// <summary>
    /// 提前安装
    /// </summary>
    /// <returns></returns>
    Task AdvanceInstallGameResourceAsync();

    #endregion

    Task StartInstallGameResource(InstallOption option);

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

    #region 选择登录账号
    Task<KRSDKGameTokenModel?> GetSDKGameTokenAsync(
        CancellationToken token = default
    );

    Task<bool> SetCurrentLoginSdkToken(
        KRSDKGameTokenCache cache,
        CancellationToken token = default
    );
    #endregion
}
