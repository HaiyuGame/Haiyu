using System.Net.Http.Headers;
using System.Net.Http.Json;
using Waves.Api.Models;
using Waves.Api.Models.GameWikiiClient;
using Waves.Core.Helpers;

namespace Waves.Core.Services;

partial class KuroClient
{
    public async Task<WikiHomeModel> GetMainWikiAsync(CancellationToken token = default)
    {
        var header = new Dictionary<string, string>()
        {
            { "devcode", AccountService.Current?.TokenDid?? "" },
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
