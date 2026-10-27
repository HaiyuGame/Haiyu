using System.Net.Http.Json;
using System.Web;
using Haiyu.Plugin.Contracts;
using Waves.Api.Models.CloudGame;
using ZLinq;

namespace Haiyu.Services;

public class WavesRecordManager
{
    public IWavesPlayerCardCacheServices CacheService { get; set; }

    public string DefaultRecordUrl => "https://gmserver-api.aki-game2.com/gacha/record/query";

    public async Task CreateDefaultRecordAsync(CreateWavesRecordRequest request)
    {
        var querys = FormatUrl(request.RecordLink);
        using HttpClient hClient = new();
        WavesAnalysisPlayerCard cards = new();
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
        cards.UpdateType = "GameContext";
        cards.UpdateSource = $"{querys["player_id"]}-{request.GameContextName}";
        await CacheService.SaveAsync(cards);
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
