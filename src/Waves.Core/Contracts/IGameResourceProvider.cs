namespace Waves.Core.Contracts;

/// <summary>
/// 游戏资源控制器，用来适配不同的服务器数据结构
/// </summary>
public interface IGameResourceProvider
{
    public GameLocalConfig LocalConfig { get; }
    public KuroGameApiConfig ApiConfig { get;  }

    public void SetConfig(GameLocalConfig gameLocal, KuroGameApiConfig apiConfig);

    /// <summary>
    /// 检查更新
    /// </summary>
    /// <returns></returns>
    public Task<bool> CheckUpdateAsync(CancellationToken token = default);

    /// <summary>
    /// 获取游戏安装资源列表
    /// </summary>
    /// <returns></returns>
    public Task<GameVersionInfo> GetInstallGameResourceAsync(CancellationToken token = default);

    /// <summary>
    /// 获取游戏更新资源列表
    /// </summary>
    /// <returns></returns>
    public Task<GameVersionInfo> GetUpdateGameResourceAsync(CancellationToken token = default);

    /// <summary>
    /// 获取游戏预下载资源列表
    /// </summary>
    /// <returns></returns>
    public Task<GameVersionInfo> GetGameProdownloadResourceAsync(CancellationToken token = default);

}
