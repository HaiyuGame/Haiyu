namespace Waves.Core.Services.GameResourceProvider;

/// <summary>
/// 鸣潮资源管理器(资源包分级)
/// </summary>
public sealed class BundleGameResourceProvider : IGameResourceProvider
{
    public GameLocalConfig LocalConfig { get; private set; } = null!;
    public KuroGameApiConfig ApiConfig { get; private set; } = null!;

    public void SetConfig(GameLocalConfig gameLocal, KuroGameApiConfig apiConfig)
    {
        LocalConfig = gameLocal ?? throw new ArgumentNullException(nameof(gameLocal));
        ApiConfig = apiConfig ?? throw new ArgumentNullException(nameof(apiConfig));
    }
    public Task<bool> CheckUpdateAsync(CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public Task<GameVersionInfo> GetGameProdownloadResourceAsync(CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public Task<GameVersionInfo> GetInstallGameResourceAsync(CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public Task<GameVersionInfo> GetUpdateGameResourceAsync(CancellationToken token = default)
    {
        throw new NotImplementedException();
    }
}
