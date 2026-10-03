namespace Waves.Core.GameContext
{
    partial class KuroGameContextBaseV2
    {
        public virtual async Task<GameLauncherSource?> GetGameLauncherSourceAsync(
            KuroGameApiConfig apiConfig = null,
            CancellationToken token = default
        )
        {
            var cacheConfig = apiConfig ?? this.Config;
            var address = GetLauncherHeaderUrl();
            var url = $"{address}/launcher/game/{cacheConfig.GameID}/{cacheConfig.AppId}_{cacheConfig.AppKey}/index.json?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            try
            {
                var result = await HttpClientService.HttpClient.GetAsync(url);
                var jsonStr = await result.Content.ReadAsStringAsync();
                var laucherIndex = await result.Content.ReadFromJsonAsync<GameLauncherSource>(
                    GameLauncherSourceContext.Default.GameLauncherSource
                );
                return laucherIndex;
            }
            catch (Exception ex)
            {
                Logger.WriteError($"请求{url}出错：{ex.Message}");
                SystemEventPublisher.Publish(new() { Message = $"请求{url}出错：{ex.Message}" });
                return null;
            }
        }

        public async Task<IndexGameResource?> GetGameResourceAsync(
            string url,
            CancellationToken token = default
        )
        {
            var result = await HttpClientService.HttpClient.GetAsync(url, token);
            return await result
                .Content.ReadFromJsonAsync<IndexGameResource>(
                    IndexGameResourceContext.Default.IndexGameResource,
                    token
                );
        }

        public async Task<PatchIndexGameResource?> GetPatchGameResourceAsync(
            string url,
            CancellationToken token = default
        )
        {
            try
            {
                var result = await HttpClientService.HttpClient.GetAsync(url, token);
                result.EnsureSuccessStatusCode();
                return result
                    .Content.ReadFromJsonAsync<PatchIndexGameResource>(
                        PathIndexGameResourceContext.Default.PatchIndexGameResource,
                        token
                    )
                    .Result;
            }
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
