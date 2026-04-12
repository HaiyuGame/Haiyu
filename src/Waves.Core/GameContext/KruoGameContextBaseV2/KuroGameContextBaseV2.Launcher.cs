using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Serilog.Core;
using Waves.Api.Models;
using Waves.Api.Models.Launcher;
using Waves.Core.GameContext.Contexts;
using Waves.Core.GameContext.Contexts.PRG;
using Waves.Core.GameContext.ContextsV2.Waves;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Downloader;
using Waves.Core.Services;

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
            var url = "";
            try
            {
                if (this.ContextName == nameof(PunishGlobalGameContext)
                    || this.ContextName == nameof(PunishTwGameContext)
                || this.ContextName == nameof(WavesGlobalGameContextV2)
                )
                {
                    url =
                        $"{KuroGameApiConfig.BaseAddress[1]}/launcher/game/{cacheConfig.GameID}/{cacheConfig.AppId}_{cacheConfig.AppKey}/index.json?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                }
                else
                {
                    url =
                        $"{KuroGameApiConfig.BaseAddress[0]}/launcher/game/{cacheConfig.GameID}/{cacheConfig.AppId}_{cacheConfig.AppKey}/index.json?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                }
                var result = await HttpClientService.HttpClient.GetAsync(url);
                var jsonStr = await result.Content.ReadAsStringAsync();
                var laucherIndex =  await result.Content.ReadFromJsonAsync<GameLauncherSource>(
                    GameLauncherSourceContext.Default.GameLauncherSource
                );
               
                #region 测试
                //launcherIndex.Predownload = new Predownload()
                //{
                //    Changelog = launcherIndex.ResourceDefault.Changelog,
                //    Config = launcherIndex.ResourceDefault.Config,
                //    Resources = launcherIndex.ResourceDefault.Resources,
                //    ResourcesBasePath = launcherIndex.ResourceDefault.ResourcesBasePath,
                //    ResourcesDiff = launcherIndex.ResourceDefault.ResourcesDiff,
                //    ResourcesExcludePath = launcherIndex.ResourceDefault.ResourcesExcludePath,
                //    ResourcesExcludePathNeedUpdate = launcherIndex
                //        .ResourceDefault
                //        .ResourcesExcludePathNeedUpdate,
                //    SampleHashSwitch = launcherIndex.ResourceDefault.SampleHashSwitch,
                //    Version = launcherIndex.ResourceDefault.Version,
                //};
                #endregion
                return laucherIndex;
            }
            catch (Exception ex)
            {
                Logger.WriteError($"请求{url}出错：{ex.Message}");
                return null;
            }
        }

        public async Task<IndexGameResource?> GetGameResourceAsync(
            ResourceDefault ResourceDefault,
            CancellationToken token = default
        )
        {
            var resourceIndexUrl =
                ResourceDefault.CdnList.Where(x => x.P != 0).OrderBy(x => x.P).First().Url
                + ResourceDefault.Config.IndexFile;
            var result = await HttpClientService.HttpClient.GetAsync(resourceIndexUrl, token);
            return result.Content.ReadFromJsonAsync<IndexGameResource>(
                IndexGameResourceContext.Default.IndexGameResource,
                token
            ).Result;
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
                return result.Content.ReadFromJsonAsync<PatchIndexGameResource>(
                    PathIndexGameResourceContext.Default.PatchIndexGameResource,
                    token
                ).Result;
            }
            catch (Exception ex)
            {
                Logger.WriteError($"请求{url}出错：{ex.Message}");
                return null;
            }
        }

        public virtual async Task<GameLauncherStarter?> GetLauncherStarterAsync(
            CancellationToken token = default
        )
        {
            string url = "";
            try
            {
                if ( this.ContextName == nameof(PunishGlobalGameContext)
                    || this.ContextName == nameof(PunishTwGameContext)
                || this.ContextName == nameof(WavesGlobalGameContextV2)
                )
                {
                    url =
                        $"{KuroGameApiConfig.BaseAddress[1]}/launcher/{this.Config.AppId}_{this.Config.AppKey}/{this.Config.GameID}/information/{this.Config.Language}.json?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                }
                else
                {
                    url =
                        $"{KuroGameApiConfig.BaseAddress[0]}/launcher/{this.Config.AppId}_{this.Config.AppKey}/{this.Config.GameID}/information/{this.Config.Language}.json?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                }
                var result = await HttpClientService.HttpClient.GetAsync(url, token);
                result.EnsureSuccessStatusCode();
                return result.Content.ReadFromJsonAsync<GameLauncherStarter>(
                    GameLauncherStarterContext.Default.GameLauncherStarter,
                    token
                ).Result;
            }
            catch (Exception ex)
            {
                Logger.WriteError($"请求{url}出错：{ex.Message}");
                return null;
            }
        }

        public virtual async Task<LIndex?> GetDefaultLauncherValue(
            CancellationToken token = default
        )
        {
            string url = "";
            if ( this.ContextName == nameof(PunishGlobalGameContext)
                || this.ContextName == nameof(PunishTwGameContext)
                || this.ContextName == nameof(WavesGlobalGameContextV2)
            )
            {
                url =
                    $"{KuroGameApiConfig.BaseAddress[1]}/launcher/launcher/{this.Config.AppId}_{this.Config.AppKey}/{this.Config.GameID}/index.json";
            }
            else
            {
                url =
                    $"{KuroGameApiConfig.BaseAddress[0]}/launcher/launcher/{this.Config.AppId}_{this.Config.AppKey}/{this.Config.GameID}/index.json";
            }
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
            var address = "";
            if (this.ContextName == nameof(PunishGlobalGameContext)
                || this.ContextName == nameof(PunishTwGameContext)
                || this.ContextName == nameof(WavesGlobalGameContextV2)
            )
            {
                address = $"{KuroGameApiConfig.BaseAddress[1]}";
            }
            else
            {
                address = $"{KuroGameApiConfig.BaseAddress[0]}";
            }
            address +=
                $"/launcher/{this.Config.AppId}_{this.Config.AppKey}/{this.Config.GameID}/background/{backgroundCode}/{this.Config.Language}.json";
            var result = await HttpClientService.HttpClient.GetAsync(address, token);
            result.EnsureSuccessStatusCode();
            return await result.Content.ReadFromJsonAsync<LauncherBackgroundData>(
                LauncherConfig.Default.LauncherBackgroundData,
                token
            );
        }
    }
}
