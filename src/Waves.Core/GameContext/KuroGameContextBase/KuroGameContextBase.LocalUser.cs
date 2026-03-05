using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Waves.Api.Models;
using Waves.Api.Models.Launcher;
using Waves.Core.GameContext.Contexts;

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
                if (count > 5)
                {
                    break;
                }
                HttpRequestMessage msg = new HttpRequestMessage();
                msg.RequestUri = new Uri(
                    $"https://pc-launcher-sdk-api.kurogame.com/game/queryPlayerInfo?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"
                );
                msg.Method = HttpMethod.Post;
                QueryLocalPlayerInfoRequest request = new QueryLocalPlayerInfoRequest();
                request.OAutoCode = oAutoCode;
                var json = JsonSerializer.Serialize(
                    request,
                    LocalGameUserContext.Default.QueryLocalPlayerInfoRequest
                );
                msg.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var reponse = await client.SendAsync(msg, token);
                var resultJson = await reponse.Content.ReadAsStringAsync(token);
                var models = JsonSerializer.Deserialize<QueryPlayerInfo>(
                    resultJson,
                    LocalGameUserContext.Default.QueryPlayerInfo
                );
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
            foreach (var item in info.Data)
            {
                QueryPlayerItem? model = JsonSerializer.Deserialize<QueryPlayerItem>(
                    item.Value,
                    LocalGameUserContext.Default.QueryPlayerItem
                );
                if (model == null)
                    continue;
                model.ServerName = item.Key;
                info.Items.Add(model);
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
                msg.Headers.Add("sec-ch-ua-platform", "Windows");
                msg.Headers.Add(
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36 Edg/133.0.0.0"
                );
                msg.Headers.Add("Accept", "application/json, text/plain, */*");
                msg.Headers.Add(
                    "sec-ch-ua",
                    "\"Chromium\";v=\"133\", \"Microsoft Edge WebView2\";v=\"133\", \"Not(A:Brand\";v=\"99\", \"Microsoft Edge\";v=\"133\""
                );
                msg.Headers.Add("sec-ch-ua-mobile", "?0");
                msg.Headers.Add("Origin", "null");
                msg.Headers.Add("Sec-Fetch-Site", "cross-site");
                msg.Headers.Add("Sec-Fetch-Mode", "cors");
                msg.Headers.Add("Sec-Fetch-Dest", "empty");
                msg.Headers.Add("Accept-Encoding", "gzip, deflate, br, zstd");
                msg.Headers.Add(
                    "Accept-Language",
                    "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6"
                );
                if (this.ContextName == nameof(WavesGlobalGameContext))
                {
                    msg.RequestUri = new Uri(
                        $"https://pc-launcher-sdk-api.kurogame.net/game/queryRole?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"
                    );
                }
                else
                {
                    msg.RequestUri = new Uri(
                        $"https://pc-launcher-sdk-api.kurogame.com/game/queryRole?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"
                    );
                }
                msg.Method = HttpMethod.Post;
                QueryLocalRoleInfoRequest request = new QueryLocalRoleInfoRequest();
                request.OAutoCode = oautoCode;
                request.PlayerId = long.Parse(playerId);
                request.Region = region;
                var json = JsonSerializer.Serialize(
                    request,
                    LocalGameUserContext.Default.QueryLocalRoleInfoRequest
                );
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
                LocalGameRoleItem? roleItem = JsonSerializer.Deserialize<LocalGameRoleItem>(
                    item.Value,
                    LocalGameUserContext.Default.LocalGameRoleItem
                );
                if (roleItem == null)
                    continue;
                roleItem.ServerName = item.Key;
                info.Items.Add(roleItem);
            }
           
        }
        return info;
    }
}
