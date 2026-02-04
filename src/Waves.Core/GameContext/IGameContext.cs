using Waves.Api.Models;
using Waves.Api.Models.Launcher;
using Waves.Core.Contracts;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext;

/// <summary>
/// 游戏核心管理
/// </summary>
public interface IGameContext
{
    public string GameContextNameKey { get; }
    public IHttpClientService HttpClientService { get; set; }

    public Task InitAsync();
    public string ContextName { get; }
    event GameContextOutputDelegate GameContextOutput;
    event GameContextProdOutputDelegate GameContextProdOutput;
    public string GamerConfigPath { get; internal set; }
    GameLocalConfig GameLocalConfig { get; }


    public GameAPIConfig Config { get; }

    public GameType GameType { get; }
    Task<FileVersion> GetLocalDLSSAsync();
    Task<FileVersion> GetLocalDLSSGenerateAsync();
    Task<FileVersion> GetLocalXeSSGenerateAsync();
    public Type ContextType { get; }

    public TimeSpan GetGameTime();

    public Task RepirGameAsync();

    #region Launcher
    Task<GameLauncherSource?> GetGameLauncherSourceAsync(GameAPIConfig apiConfig = null,CancellationToken token = default);

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
    Task StartDownloadTaskAsync(string folder, GameLauncherSource? source,bool isDelete = false);

    /// <summary>
    /// 进行预下载
    /// </summary>
    /// <param name="launcher"></param>
    /// <param name="downloadFolder"></param>
    /// <returns></returns>
    Task<bool> StartDownloadProdGame(string downloadFolder);

    /// <summary>
    /// 恢复任务
    /// </summary>
    /// <returns></returns>
    Task<bool> ResumeDownloadAsync();

    /// <summary>
    /// 取消下载
    /// </summary>
    /// <returns></returns>
    Task<bool> StopDownloadAsync();

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
    Task SetSpeedLimitAsync(long bytesPerSecond);

    /// <summary>
    /// 获得游戏登陆的OAuth的代码
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<List<KRSDKLauncherCache>?> GetLocalGameOAuthAsync(CancellationToken token);
    /// <summary>
    /// 安装预下载内容
    /// </summary>
    /// <param name="diffFolder"></param>
    /// <returns></returns>
    Task<bool> StartInstallPredGame(string diffFolder);
    Task<bool> StartGameAsync();
    Task UpdataGameAsync(string diffSavePath = null);
    Task StopGameAsync();
    Task DeleteResourceAsync();
    #endregion

    Task<LIndex?> GetDefaultLauncherValue(CancellationToken token = default);

    Task<LauncherBackgroundData?> GetLauncherBackgroundDataAsync(string backgroundCode, CancellationToken token = default);
}
