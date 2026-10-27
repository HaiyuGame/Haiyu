using System.Net.Http.Json;
using System.Web;
using Haiyu.Plugin.Contracts;
using Haiyu.Plugin.Services;
using Waves.Api.Models.CloudGame;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.GameContext.ContextsV2.Waves;
using ZLinq;

namespace Haiyu.Services;

/// <summary>
/// 使用V3版本，此类专门为游戏核心创建一个档案入口，云鸣潮档案入口后续迁移至此
/// </summary>
public class WavesRecordManager
{
    public IWavesPlayerCardCacheServices CacheService { get; set; }
    public IWavesCloudGameService WavesCloudGameService { get; }

    public WavesRecordManager(
        [FromKeyedServices($"{nameof(WavesPlayerCardCacheServices)}V3")]
            IWavesPlayerCardCacheServices wavesPlayerCardCacheServices,
        IWavesCloudGameService wavesCloudGameService
    )
    {
        this.CacheService = wavesPlayerCardCacheServices;
        WavesCloudGameService = wavesCloudGameService;
    }

    public string DefaultRecordUrl => "https://gmserver-api.aki-game2.com/gacha/record/query";

    public string GlobalRecordUrl => "https://gmserver-api.aki-game2.net/gacha/record/query";

    /// <summary>
    /// 离线文件档案入口
    /// </summary>
    /// <param name="request">本地文件请求创建</param>
    /// <returns></returns>
    public async Task CreateDefaultRecordAsync(
        CreateWavesRecordRequest request,
        CancellationToken token = default
    )
    {
        var querys = FormatUrl(request.RecordLink);
        var url =
            request.GameContextName == nameof(WavesGlobalGameContextV2)
                ? GlobalRecordUrl
                : DefaultRecordUrl;
        using HttpClient hClient = new();
        WavesAnalysisPlayerCard cards = new();
        cards.Items = [];
        foreach (var item in CardPoolTypeValues.All)
        {
            PlayerCard payload = new PlayerCard()
            {
                CardPoolId = querys["resources_id"],
                CardPoolType = (int)item,
                PlayerId = querys["player_id"],
                ServerId = querys["svr_id"],
                RecordId = querys["record_id"],
                LanguageCode = querys["lang"],
            };
            var httpRequest = new HttpRequestMessage()
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload, CommunityContext.Default.PlayerCard),
                    new MediaTypeHeaderValue("application/json")
                ),
                Method = HttpMethod.Post,
                RequestUri = new(DefaultRecordUrl),
            };
            var reponse = await hClient.SendAsync(httpRequest);
            var result = await reponse.Content.ReadFromJsonAsync<PlayerReponse>(
                CommunityContext.Default.PlayerReponse
            );
            if (result?.Data != null)
            {
                cards.Items.Add(
                    new WavesAnalysisPlayerCardItem()
                    {
                        PoolType = (int)item,
                        Resource = result.Data.Select(x => new RecordCardItemWrapper(x)),
                    }
                );
            }
        }

        cards.SessionId = querys["player_id"];
        cards.UpdateType = "GameContext";
        cards.UpdateSource = $"{querys["player_id"]}-{request.GameContextName}";
        cards.Version = 2;
        cards.RecordName = request.RecordName;
        await CacheService.SaveAsync(cards);
    }

    /// <summary>
    /// 云鸣潮档案入口
    /// </summary>
    /// <param name="session">云鸣潮登录信息</param>
    /// <returns></returns>
    public async Task CreateCloudRecordAsync(
        CloudGameLoginSession session,
        string recordName,
        CancellationToken token = default
    )
    {
        var record = await WavesCloudGameService.GetRecordAsync(session);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(record.Data);
        WavesAnalysisPlayerCard cards = new()
        {
            Items = new List<WavesAnalysisPlayerCardItem>(),
            LastUpdater = DateTime.Now,
            SessionId = record.Data.PlayerId.ToString(),
            RecordName = recordName
        };
        foreach (var item in CardPoolTypeValues.All)
        {
            var resources = await this.WavesCloudGameService.GetGameRecordResource(
                session,
                record.Data.RecordId,
                record.Data.PlayerId.ToString(),
                (int)item
            );
            if (resources?.Data != null)
            {
                cards.Items.Add(
                    new WavesAnalysisPlayerCardItem()
                    {
                        PoolType = (int)item,
                        Resource = resources.Data.Select(x => new RecordCardItemWrapper(x)),
                    }
                );
            }
        }
        await this.CacheService.SaveAsync(cards);
    }

    public async Task<List<WavesAnalysisPlayerCard>> GetLocalWavesRecordsAsync(
        CancellationToken token = default
    )
    {
        return await CacheService.LoadAllAsync();
    }

    private NameValueCollection FormatUrl(string url)
    {
        var linkUri = new System.Uri(url);
        var query = linkUri.Fragment.Split('?', 2);
        return HttpUtility.ParseQueryString(query.Length > 1 ? query[1] : "");
    }
}

public class CreateWavesRecordRequest
{
    public string RecordName { get; set; }

    public string RecordLink { get; set; }

    public string GameContextName { get; set; }
}
