using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using Waves.Api.Models;
using Waves.Api.Models.Enums;
using Waves.Api.Models.GameWikiiClient;
using Waves.Core.Contracts;

namespace Waves.Core.Services;

public sealed partial class GameWikiClient : IGameWikiClient
{
    public async Task<WikiHomeModel?> GetHomePageAsync(
        WikiType type,
        CancellationToken token = default
    )
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                var request = new HttpRequestMessage();
                request.RequestUri = new Uri("https://api.kurobbs.com/wiki/core/homepage/getPage");
                request.Headers.Add("wiki_type", ((int)type).ToString());
                request.Method = HttpMethod.Post;
                var response = await client.SendAsync(request, token);
                var json = await response.Content.ReadAsStringAsync();
                var model = await response.Content.ReadFromJsonAsync<WikiHomeModel>(
                    WikiContext.Default.WikiHomeModel,
                    token
                );
                return model;
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 获得活动事件数据
    /// </summary>
    /// <returns></returns>
    public async Task<List<HotContentSide?>> GetEventDataAsync(
        WikiType type,
        CancellationToken token = default
    )
    {
        try
        {
            var model = await GetHomePageAsync(type, token).ConfigureAwait(false);
            if (model == null)
                return null;
            var dataString = model
                .Data.ContentJson.SideModules.Where(x => x.Type == "hot-content-side")
                .FirstOrDefault();
            if (dataString == null)
                return null;
            if (dataString.Content is JsonElement element)
            {
                var result = element.Deserialize(WikiContext.Default.ListHotContentSide);
                return result;
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<EventContentSide?> GetEventTabDataAsync(
        WikiType type,
        CancellationToken token = default
    )
    {
        try
        {
            var model = await GetHomePageAsync(type, token);
            if (model == null)
                return null;
            var dataString = model
                .Data.ContentJson.SideModules.Where(x => x.Type == "events-side")
                .FirstOrDefault();
            if (dataString == null)
                return null;
            if (dataString.Content is JsonElement element)
            {
                var result = element.Deserialize(WikiContext.Default.EventContentSide);
                return result;
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public List<HotContentSide>? GetEventData(WikiHomeModel model)
    {
        var dataString = model
                .Data.ContentJson.SideModules.Where(x => x.Type == "hot-content-side");
        if (dataString == null)
            return null;
        if (dataString == null)
            return null;
        if (dataString.First().Content is JsonElement element)
        {
            var result = element.Deserialize(WikiContext.Default.ListHotContentSide);
            return result;
        }
        return null;
    }
}
