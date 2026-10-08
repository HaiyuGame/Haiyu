using Waves.Core.Services.GameResourceProvider;

namespace Waves.Core.GameContext
{
    partial class KuroGameContextBaseV2
    {
        public Task<bool> CheckUpdateAsync(CancellationToken token = default) =>
            GameResourceProvider.CheckUpdateAsync(token);

        public Task<GameVersionInfo> GetInstallGameResourceAsync(CancellationToken token = default) =>
            GameResourceProvider.GetInstallGameResourceAsync(token);

        public Task<GameVersionInfo> GetUpdateGameResourceAsync(CancellationToken token = default) =>
            GameResourceProvider.GetUpdateGameResourceAsync(token);

        public Task<GameVersionInfo> GetGameProdownloadResourceAsync(CancellationToken token = default) =>
            GameResourceProvider.GetGameProdownloadResourceAsync(token);

        // 旧 DTO 入口暂时保留；请求实现统一交给 Legacy Provider。
        private LegacyGameResourceProvider GetLegacyResourceProvider(KuroGameApiConfig? apiConfig = null)
        {
            if (apiConfig is null && GameResourceProvider is LegacyGameResourceProvider legacy)
                return legacy;
            var provider = new LegacyGameResourceProvider(HttpClientService);
            provider.SetConfig(GameLocalConfig, apiConfig ?? Config);
            return provider;
        }

        public virtual async Task<GameLauncherSource?> GetGameLauncherSourceAsync(
            KuroGameApiConfig apiConfig = null,
            CancellationToken token = default)
        {
            try
            {
                return await GetLegacyResourceProvider(apiConfig).GetGameLauncherSourceAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                Logger.WriteError($"获取游戏资源配置出错：{ex.Message}");
                SystemEventPublisher.Publish(new() { Message = $"获取游戏资源配置出错：{ex.Message}" });
                return null;
            }
        }

        public async Task<IndexGameResource?> GetGameResourceAsync(
            string url, CancellationToken token = default) =>
            await GetLegacyResourceProvider().GetGameResourceAsync(url, token);

        public async Task<PatchIndexGameResource?> GetPatchGameResourceAsync(
            string url, CancellationToken token = default)
        {
            try
            {
                return await GetLegacyResourceProvider().GetPatchGameResourceAsync(url, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                Logger.WriteError($"请求{url}出错：{ex.Message}");
                SystemEventPublisher.Publish(new() { Message = $"请求{url}出错：{ex.Message}" });
                return null;
            }
        }

        public virtual async Task<GameLauncherStarter?> GetLauncherStarterAsync(
            CancellationToken token = default
        )
        {
            var address = GetLauncherHeaderUrl();
            string url = $"{address}/launcher/{this.Config.AppId}_{this.Config.AppKey}/{this.Config.GameID}/information/{this.Config.Language}.json?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            try
            {
                
                var result = await HttpClientService.HttpClient.GetAsync(url, token);
                result.EnsureSuccessStatusCode();
                return await result.Content.ReadFromJsonAsync<GameLauncherStarter>(
                    GameLauncherStarterContext.Default.GameLauncherStarter,
                    token
                );
            }
            catch (Exception ex)
            {
                Logger.WriteError($"请求{url}出错：{ex.Message}");
                SystemEventPublisher.Publish(new() { Message = $"请求{url}出错：{ex.Message}" });
                return null;
            }
        }

        public virtual async Task<LIndex?> GetDefaultLauncherValue(
            CancellationToken token = default
        )
        {
            var address = GetLauncherHeaderUrl();
            string url = $"{address}/launcher/launcher/{this.Config.AppId}_{this.Config.AppKey}/{this.Config.GameID}/index.json";
            
            var result = await HttpClientService.HttpClient.GetAsync(url, token);
            result.EnsureSuccessStatusCode();
            return await result.Content.ReadFromJsonAsync<LIndex>(
                LauncherConfig.Default.LIndex,
                token
            );
        }

        public virtual async Task<LauncherBackgroundData?> GetLauncherBackgroundDataAsync(
            string backgroundCode,
            CancellationToken token = default
        )
        {

            var address = GetLauncherHeaderUrl();
            
             address +=
                $"/launcher/{this.Config.AppId}_{this.Config.AppKey}/{this.Config.GameID}/background/{backgroundCode}/{this.Config.Language}.json";
            var result = await HttpClientService.HttpClient.GetAsync(address, token);
            result.EnsureSuccessStatusCode();
            return await result.Content.ReadFromJsonAsync<LauncherBackgroundData>(
                LauncherConfig.Default.LauncherBackgroundData,
                token
            );
        }

        private string GetLauncherHeaderUrl()
        {
            if (
                this.ContextName == nameof(PunishGlobalGameContextV2)
                || this.ContextName == nameof(PunishTwGameContextV2)
                || this.ContextName == nameof(WavesGlobalGameContextV2)
            )
            {
                return $"{KuroGameApiConfig.BaseAddress[1]}";
            }
            else
            {
                return $"{KuroGameApiConfig.BaseAddress[0]}";
            }
        }

        public virtual async Task<LauncherHeader?> GetLauncherHeaderAsync(
            CancellationToken token = default)
        {
            return new();
        }
    }
}
