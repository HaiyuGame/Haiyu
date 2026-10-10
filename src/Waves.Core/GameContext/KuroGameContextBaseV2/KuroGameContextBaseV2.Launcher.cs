using Waves.Core.Models.Options;

using Waves.Core.Services.GameResourceProvider;

namespace Waves.Core.GameContext
{
    partial class KuroGameContextBaseV2
    {
        public Task<bool> CheckUpdateAsync(GameResourceParameter? parameter = null, CancellationToken token = default) =>
            GameResourceProvider.CheckUpdateAsync(parameter, token);

        public Task<GameVersionInfo> GetInstallGameResourceAsync(GameResourceParameter? parameter = null, CancellationToken token = default) =>
            GameResourceProvider.GetInstallGameResourceAsync(parameter, token);

        public Task<GameVersionInfo> GetUpdateGameResourceAsync(GameResourceParameter? parameter = null, CancellationToken token = default) =>
            GameResourceProvider.GetUpdateGameResourceAsync(parameter, token);

        public Task<GameVersionInfo> GetGameProdownloadResourceAsync(GameResourceParameter? parameter = null, CancellationToken token = default) =>
            GameResourceProvider.GetGameProdownloadResourceAsync(parameter, token);

        public Task<GameResourceSummary> GetResourceSummaryAsync(GameResourceParameter? parameter = null, CancellationToken token = default) =>
            GameResourceProvider.GetResourceSummaryAsync(parameter, token);

        public Task<GameVersionInfo> GetVerificationResourceAsync(string targetVersion, GameResourceParameter? parameter = null, CancellationToken token = default) =>
            GameResourceProvider.GetVerificationResourceAsync(targetVersion, parameter, token);

        public virtual async Task<GameLauncherStarter?> GetLauncherStarterAsync(
            CancellationToken token = default
        )
        {
            try
            {
                
                return await GameResourceProvider.GetLauncherStarterAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                Logger.WriteError($"获取启动器公告出错：{ex.Message}");
                SystemEventPublisher.Publish(new() { Message = $"获取启动器公告出错：{ex.Message}" });
                return null;
            }
        }

        public virtual Task<LIndex?> GetDefaultLauncherValue(CancellationToken token = default) =>
            GameResourceProvider.GetDefaultLauncherValue(token);

        public virtual Task<LauncherBackgroundData?> GetLauncherBackgroundDataAsync(
            string backgroundCode, CancellationToken token = default) =>
            GameResourceProvider.GetLauncherBackgroundDataAsync(backgroundCode, token);
        public virtual async Task<LauncherHeader?> GetLauncherHeaderAsync(
            CancellationToken token = default)
        {
            return new();
        }
    }
}
