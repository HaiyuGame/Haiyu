namespace Haiyu.KuroClient;

partial class KuroClient
{
    public async Task<GamerDataModel?> GetGamerDataAsync(
        KuroAccount account,
        GameRoilDataItem gamerRoil,
        CancellationToken token = default
    )
    {
        var header = GetDeviceHeader(account);
        var queryData = new Dictionary<string, string>()
        {
            { "gameId", "3" },
            { "serverId", gamerRoil.ServerId },
            { "userId", gamerRoil.UserId.ToString() },
            { "roleId", gamerRoil.RoleId.ToString() },
            { "type", "1" },
            { "sizeType", "2" },
        };
        var request = await BuildRequestAsync(
            "https://api.kurobbs.com/gamer/widget/game3/getData",
            HttpMethod.Post,
            header,
            new("application/x-www-form-urlencoded"),
            queryData,
            true
        );
        var result = await HttpClient.SendAsync(request);
        string jsonStr = await result.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize(jsonStr, CommunityContext.Default.GamerDataModel);
    }

    public async Task<GamerRoil?> GetGamerAsync(
        KuroAccount account,
        int gameId,
        CancellationToken token = default
    )
    {
        var header = GetDeviceHeader(account);
        var content = new Dictionary<string, string>()
        {
            { "gameId", gameId.ToString() },
        };
        var request = await BuildRequestAsync(
            "https://api.kurobbs.com/gamer/role/list",
            HttpMethod.Post,
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded", "utf-8"),
            content,
            true,
            token
        );
        var result = await HttpClient.SendAsync(request);
        var jsonStr = await result.Content.ReadAsStringAsync();
        return (GamerRoil?)JsonSerializer.Deserialize(jsonStr, CommunityContext.Default.GamerRoil);
    }

    public async Task<SMSResultModel?> SendSMSAsync(
        string mobile,
        string geeTestData,
        string tokenDid,
        CancellationToken token = default
    )
    {
        var header = new Dictionary<string, string>()
        {
            { "osVersion", "Android" },
            { "devCode", tokenDid },
            { "distinct_id", "e0f62c50-4c62-4983-9f6a-bf96f3566095" },
            { "countryCode", "CN" },
            { "model", "23127PN0CC" },
            { "source", "android" },
            { "lang", "zh-Hans" },
            { "version", "3.1.2" },
            { "channelId", "2" },
            { "Accept-Encoding", "gzip" },
            { "User-Agent", "okhttp/3.11.0" },
        };
        var query = new Dictionary<string, string>()
        {
            { "mobile", mobile },
            { "geeTestData", geeTestData },
        };
        var request = await BuildLoginRequest(
            "https://api.kurobbs.com/user/getSmsCode",
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            query
        );
        var result = await this.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        return (SMSResultModel?)
            JsonSerializer.Deserialize(jsonStr, CommunityContext.Default.SMSResultModel);
    }

    public async Task<AccountModel?> LoginAsync(
        string mobile,
        string code,
        string tokenDid,
        CancellationToken token = default
    )
    {
        var header = new Dictionary<string, string>()
        {
            { "osVersion", "Android" },
            { "devCode",tokenDid },
            { "distinct_id", "96b1567b-b5e6-422f-a1dd-7cb1e58c5db7" },
            { "countryCode", "CN" },
            { "model", "android" },
            { "source", "android" },
            { "lang", "zh-Hans" },
            { "version", "2.2.0" },
            { "versionCode", "2200" },
            { "channelId", "2" },
            { "Accept-Encoding", "gzip" },
            { "User-Agent", "okhttp/3.11.0" },
        };
        var query = new Dictionary<string, string>()
        {
            { "mobile", mobile },
            { "devCode", tokenDid },
            { "code", code },
        };
        var request = await BuildLoginRequest(
            "https://api.kurobbs.com/user/sdkLogin",
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            query
        );
        var result = await this.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        return JsonSerializer.Deserialize(jsonStr, CommunityContext.Default.AccountModel);
    }
}
