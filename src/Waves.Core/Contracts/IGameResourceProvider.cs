using Waves.Core.Models.Options;

namespace Waves.Core.Contracts;

/// <summary>
/// 游戏资源控制器，用来适配不同的服务器数据结构
/// </summary>
public interface IGameResourceProvider
{
    Task<GameLauncherStarter?> GetLauncherStarterAsync(CancellationToken token = default);
    Task<LIndex?> GetDefaultLauncherValue(CancellationToken token = default);
    Task<LauncherBackgroundData?> GetLauncherBackgroundDataAsync(string backgroundCode, CancellationToken token = default);

    Task<GameResourceSummary> GetResourceSummaryAsync(GameResourceParameter? parameter = null, CancellationToken token = default);
    Task<GameVersionInfo> GetVerificationResourceAsync(string targetVersion, GameResourceParameter? parameter = null, CancellationToken token = default);
    public GameLocalConfig LocalConfig { get; }
    public KuroGameApiConfig ApiConfig { get;  }

    public void SetConfig(GameLocalConfig gameLocal, KuroGameApiConfig apiConfig);

    /// <summary>
    /// 检查更新
    /// </summary>
    /// <returns></returns>
    public Task<bool> CheckUpdateAsync(GameResourceParameter? parameter = null, CancellationToken token = default);

    /// <summary>
    /// 获取游戏安装资源列表
    /// </summary>
    /// <returns></returns>
    public Task<GameVersionInfo> GetInstallGameResourceAsync(GameResourceParameter? parameter = null, CancellationToken token = default);

    /// <summary>
    /// 获取游戏更新资源列表
    /// </summary>
    /// <returns></returns>
    public Task<GameVersionInfo> GetUpdateGameResourceAsync(GameResourceParameter? parameter = null, CancellationToken token = default);

    /// <summary>
    /// 获取游戏预下载资源列表
    /// </summary>
    /// <returns></returns>
    public Task<GameVersionInfo> GetGameProdownloadResourceAsync(GameResourceParameter? parameter = null, CancellationToken token = default);

}
