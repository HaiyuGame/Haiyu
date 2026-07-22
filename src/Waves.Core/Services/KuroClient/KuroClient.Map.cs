namespace Waves.Core.Services;

partial class KuroClient
{
    public HttpClient MapClient { get; private set; }
    private KuroAccount? _mapAccount;

    public async Task InitMapPostion(KuroAccount account)
    {
        _mapAccount = account;
        MapClient = new HttpClient()
        {
            BaseAddress = new Uri("https://api.kurobbs.com")
        };
        if (!(await MapPreCheckAsync()))
        {
            return;
        }
        var user = await GetKuroRoleBindingInfoAsync();
        WebSocketMapClient client = new WebSocketMapClient();
        await client.StartAsync(BuildUri());
    }

    public string BuildUri()
    {
        var builder = new UriBuilder("wss://api.kurobbs.com/ws-map");
        var account = _mapAccount ?? throw new InvalidOperationException("地图客户端尚未初始化账号。");
        var query = $"devcode={account.DeviceId}&token={account.Token}&source=android";
        builder.Query = query;
        //wss://api.kurobbs.com/ws-map?devcode=v3gMmf9EnuSrdMCgZHrxauEWB2VZoyEj&token=eyJhbGciOiJIUzI1NiJ9.eyJjcmVhdGVkIjoxNzc4NDI1OTAwNjU5LCJ1c2VySWQiOjE3MzAxNDg0fQ.G0viXGZuvAKWLizzeDR15ocCYQO6ktEXj0TSsDH4hrE&source=android
        return builder.Uri.AbsoluteUri;
    }

    public async Task<bool> MapPreCheckAsync()
    {
        var request = BuildMapRequest(HttpMethod.Post, "/map/core/gamer/role/preCheck");
        var result = await MapClient.SendAsync(request);
        var data = await result.Content.ReadFromJsonAsync(MapJsonContext.Default.MapApiResponseBoolean);
        if(data != null) 
        {
            return data.Data;
        }
        return false;
    }

    public async Task<KuroRoleBindingInfoData?> GetKuroRoleBindingInfoAsync()
    {
        var request = BuildMapRequest(HttpMethod.Post, "/map/core/gamer/role/getBindRoleInfo", null, "");
        var result = await MapClient.SendAsync(request);
        var data = await result.Content.ReadFromJsonAsync(MapJsonContext.Default.MapApiResponseKuroRoleBindingInfoData);
        if(data != null) 
        {
            return data.Data;
        }
        return null;
    }

    public HttpRequestMessage BuildMapRequest(HttpMethod method,string url,string body = null,string @paramQuery = null)
    {
        var postData = new HttpRequestMessage(method,url);
        if (_mapAccount is not null)
        {
            postData.Headers.TryAddWithoutValidation("token", _mapAccount.Token);
            postData.Headers.TryAddWithoutValidation("devcode", _mapAccount.DeviceId);
            postData.Headers.TryAddWithoutValidation("source", "android");
            postData.Headers.TryAddWithoutValidation("wiki_type", "10");
            postData.Headers.TryAddWithoutValidation("Referer", "https://www.kurobbs.com/");
        }
        if (method == HttpMethod.Post)
        {
            postData.Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json");

        }else if(method == HttpMethod.Get && !string.IsNullOrEmpty(@paramQuery))
        {
            postData.RequestUri = new Uri($"{url}?{@paramQuery}", UriKind.Relative);
        }
        return postData;
    }
}
