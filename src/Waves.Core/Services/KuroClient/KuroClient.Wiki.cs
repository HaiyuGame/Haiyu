namespace Waves.Core.Services;

partial class KuroClient
{
    public async Task<WikiHomeModel> GetMainWikiAsync(
        KuroAccount account,
        CancellationToken token = default
    )
    {
        var header = new Dictionary<string, string>()
        {
            { "devcode", account.DeviceId },
            { "wiki_type", "9" },
        };
        var request = await BuildRequestAsync(
            "https://api.kurobbs.com/wiki/core/homepage/getPage",
            HttpMethod.Post,
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded", "UTF-8"),
            [],
            false,
            token
        );
        var reponse = await HttpClientService.HttpClient.SendAsync(request, token);
        return await reponse.Content.ReadFromJsonAsync(WikiContext.Default.WikiHomeModel, token);
    }
}
