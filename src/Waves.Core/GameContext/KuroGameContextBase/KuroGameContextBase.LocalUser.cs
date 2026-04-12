using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Waves.Api.Models;
using Waves.Api.Models.Launcher;
using Waves.Core.GameContext.Contexts;
using Waves.Core.GameContext.Contexts.PRG;
using Waves.Core.GameContext.ContextsV2.Waves;
using Waves.Core.Models;

namespace Waves.Core.GameContext;

partial class KuroGameContextBase
{
    public virtual async Task<List<KRSDKLauncherCache>?> GetLocalGameOAuthAsync(
        CancellationToken token = default
    )
    {
        try
        {
            if (this.Config.PKGId == null)
            {
                return null;
            }
            var roming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var gameLocal = Path.Combine(roming, $"KR_{this.Config.GameID}");
            var gameLaunche = Path.Combine(
                gameLocal,
                $"{this.Config.PKGId}\\KRSDKUserLauncherCache.json"
            );
            if (Directory.Exists(gameLocal) && File.Exists(gameLaunche))
            {
                var fileStr = await File.ReadAllTextAsync(gameLaunche, token);
                var model = JsonSerializer.Deserialize(
                    fileStr,
                    LauncherConfig.Default.ListKRSDKLauncherCache
                );
                return model;
            }
            return null;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    /// <summary>
    /// 获取账号详情,自动重试5次
    /// </summary>
    /// <param name="oAutoCode">解密后数据</param>
    /// <param name="token">释放令牌</param>
    /// <returns></returns>
    public async Task<QueryPlayerInfo?> QueryPlayerInfoAsync(
        string oAutoCode,
        CancellationToken token = default
    )
    {
        using (HttpClient client = new HttpClient())
        {
            int count = 0;
            QueryPlayerInfo? info = null;
            while (true)
            {
                HttpRequestMessage msg = new HttpRequestMessage();
                var url = PlayerInfoUser();
                if (url == null)
                    return null;
                msg.RequestUri = new Uri(url);
                msg.Method = HttpMethod.Post;
                WavesQueryLocalPlayerInfoRequest request = new WavesQueryLocalPlayerInfoRequest();
                request.OAutoCode = oAutoCode;
                var json = JsonSerializer.Serialize(
                    request,
                    LocalGameUserContext.Default.WavesQueryLocalPlayerInfoRequest
                );
                msg.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var reponse = await client.SendAsync(msg, token);
                var resultJson = await reponse.Content.ReadAsStringAsync(token);
                var models = JsonSerializer.Deserialize<QueryPlayerInfo>(
                    resultJson,
                    LocalGameUserContext.Default.QueryPlayerInfo
                );
                if (count > 5)
                {
                    info = models;
                    break;
                }
                if (models == null || models.Code != 0)
                {
                    count++;
                    continue;
                }
                info = models;
                break;
            }
            if (info == null)
                return null;
            info.Items = new();
            if (info.Code != 0)
            {
                return info;
            }
            foreach (var item in info.Data)
            {
                if (this.GameType == Models.Enums.GameType.Waves)
                {
                    WavesQueryPlayerItem? model = JsonSerializer.Deserialize<WavesQueryPlayerItem>(
                        item.Value,
                        LocalGameUserContext.Default.WavesQueryPlayerItem
                    );
                    if (model == null)
                        continue;
                    model.ServerName = item.Key;
                    info.Items.Add(model);
                }
                else if (this.GameType == Models.Enums.GameType.Punish)
                {
                    PunishQueryPlayerItem? model =
                        JsonSerializer.Deserialize<PunishQueryPlayerItem>(
                            item.Value,
                            LocalGameUserContext.Default.PunishQueryPlayerItem
                        );
                    if (model == null)
                        continue;
                    model.ServerName = item.Key;
                    info.Items.Add(model);
                }
            }
            return info;
        }
    }

    public async Task<QueryRoleInfo?> QueryRoleInfoAsync(
        string oautoCode,
        string playerId,
        string region,
        CancellationToken token = default
    )
    {
        int count = 0;
        QueryRoleInfo? info = null;
        using (HttpClient client = new HttpClient())
        {
            while (true)
            {
                if (count > 5)
                    break;
                HttpRequestMessage msg = new HttpRequestMessage();

                var url = RoleInfoUser();
                if (url == null)
                    return null;
                msg.RequestUri = new Uri(url);

                msg.Method = HttpMethod.Post;
                QueryLocalRoleInfoRequest request = new QueryLocalRoleInfoRequest();
                request.OAutoCode = oautoCode;
                request.PlayerId = playerId;
                request.Region = region;
                var serializeOptions = new JsonSerializerOptions(
                    LocalGameUserContext.Default.Options
                )
                {
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                };
                var json = JsonSerializer.Serialize(request, serializeOptions);
                msg.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var reponse = await client.SendAsync(msg, token);
                var model = JsonSerializer.Deserialize<QueryRoleInfo>(
                    await reponse.Content.ReadAsStringAsync(token),
                    LocalGameUserContext.Default.QueryRoleInfo
                );
                if (model == null || model.Code == 1005 || model.Code == 1001)
                {
                    count++;
                    continue;
                }
                info = model;
                break;
            }
            if (info == null)
            {
                return null;
            }
            info.Items = [];
            foreach (var item in info.Data)
            {
                if (this.GameType == Models.Enums.GameType.Waves)
                {
                    WavesLocalGameRoleItem? roleItem =
                        JsonSerializer.Deserialize<WavesLocalGameRoleItem>(
                            item.Value,
                            LocalGameUserContext.Default.WavesLocalGameRoleItem
                        );
                    if (roleItem == null)
                        continue;
                    roleItem.ServerName = item.Key;
                    info.Items.Add(roleItem);
                }
                else if (this.GameType == Models.Enums.GameType.Punish)
                {
                    PunishLocalGameRoleItem? roleItem =
                        JsonSerializer.Deserialize<PunishLocalGameRoleItem>(
                            item.Value,
                            LocalGameUserContext.Default.PunishLocalGameRoleItem
                        );
                    if (roleItem == null)
                        continue;
                    roleItem.ServerName = item.Key;
                    info.Items.Add(roleItem);
                }
            }
        }
        return info;
    }

    public string? PlayerInfoUser()
    {
        if (this.GameType == Models.Enums.GameType.Waves)
        {
            if (this.ContextName == nameof(WavesGlobalGameContextV2))
            {
                return $"https://pc-launcher-sdk-api.kurogame.net/game/queryPlayerInfo?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
            else
            {
                return $"https://pc-launcher-sdk-api.kurogame.com/game/queryPlayerInfo?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
        }

        if (this.GameType == Models.Enums.GameType.Punish)
        {
            //https://pc-launcher-sdk-haru-api.kurogames.com/game/queryPlayerInfo?_t=1772959214
            //https://pc-launcher-sdk-haru-api.kurogames.com/game/queryRole?_t=1772959216

            if (
                this.ContextName == nameof(PunishGlobalGameContext)
                || this.ContextName == nameof(PunishTwGameContext)
            )
            {
                return $"https://pc-launcher-sdk-haru-api.kurogames.net/game/queryPlayerInfo?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
            else
            {
                return $"https://pc-launcher-sdk-haru-api.kurogames.com/game/queryPlayerInfo?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
        }

        return null;
    }

    public string? RoleInfoUser()
    {
        if (this.GameType == Models.Enums.GameType.Waves)
        {
            if (this.ContextName == nameof(WavesGlobalGameContextV2))
            {
                return $"https://pc-launcher-sdk-api.kurogame.net/game/queryRole?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
            else
            {
                return $"https://pc-launcher-sdk-api.kurogame.com/game/queryRole?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
        }

        if (this.GameType == Models.Enums.GameType.Punish)
        {
            if (
                this.ContextName == nameof(PunishGlobalGameContext)
                || this.ContextName == nameof(PunishTwGameContext)
            )
            {
                return $"https://pc-launcher-sdk-haru-api.kurogames.net/game/queryRole?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
            else
            {
                return $"https://pc-launcher-sdk-haru-api.kurogames.com/game/queryRole?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }
        }

        return null;
    }
}
