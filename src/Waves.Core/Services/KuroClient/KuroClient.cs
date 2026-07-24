namespace Waves.Core.Services;

public sealed partial class KuroClient : IKuroClient
{
    public IHttpClientService HttpClientService { get; }
    public LoggerService LoggerService { get; }
    public string Ip { get; private set; }

    public KuroClient(
        IHttpClientService httpClientService,
        [FromKeyedServices("AppLog")] LoggerService loggerService
    )
    {
        HttpClientService = httpClientService;
        LoggerService = loggerService;
        HttpClientService.BuildClient();
    }

    private static Dictionary<string, string> GetDeviceHeader(
        KuroAccount? account = null,
        string? accessToken = null
    )
    {
        var dict = new Dictionary<string, string>()
        {
            { "Accept", "application/json, text/plain, */*" },
            { "Accept-Encoding", "gzip, deflate" },
            { "Accept-Language", "zh-CN,zh;q=0.9,en-US;q=0.8,en;q=0.7" },
            { "source", "android" },
            { "devCode", account?.DeviceId ?? "" },
            //{ "model","23117RK66C"},
            { "version", "2.5.3" },
            { "lang", "zh-Hans" },
            { "countryCode", "CN" },
        };
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            dict.Add("b-at", accessToken);
        }
        if (account is not null)
        {
            dict.Add("token", account.Token);
        }
        return dict;
    }

    private Dictionary<string, string> GetWebHeader(
        KuroAccount? account = null,
        string? accessToken = null
    )
    {
        var dict = new Dictionary<string, string>()
        {
            { "Accept", "application/json, text/plain, */*" },
            { "Accept-Encoding", "gzip, deflate" },
            { "Accept-Language", "zh-CN,zh;q=0.9,en-US;q=0.8,en;q=0.7" },
            {
                "User-Agent",
                "Mozilla/5.0 (Linux; Android 12; 23117RK66C Build/V417IR; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/101.0.4951.61 Safari/537.36 Kuro/2.5.3 KuroGameBox/2.5.3"
            },
            { "did", account?.DeviceId ?? "" },
            { "source", "android" },
            {
                "devCode",
                $"{this.Ip}, Mozilla/5.0 (Linux; Android 12; 23117RK66C Build/V417IR; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/101.0.4951.61 Safari/537.36 Kuro/2.5.3 KuroGameBox/2.5.3"
            },
        };
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            dict.Add("b-at", accessToken);
        }
        if (account is not null)
        {
            dict.Add("token", account.Token);
        }
        return dict;
    }

    private async Task<HttpRequestMessage> BuildLoginRequest(
        string url,
        Dictionary<string, string> headers,
        MediaTypeHeaderValue mediatype,
        Dictionary<string, string> queryValues,
        CancellationToken token = default
    )
    {
        var request = new HttpRequestMessage();
        request.Method = HttpMethod.Post;
        foreach (var item in headers)
        {
            request.Headers.Add(item.Key, item.Value);
        }
        request.RequestUri = new Uri(url);

        var endcod = new FormUrlEncodedContent(queryValues);
        var query = await endcod.ReadAsStringAsync(token);
        request.Content = new StringContent(query, mediatype);
        return request;
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(
        string url,
        HttpMethod method,
        Dictionary<string, string> headers,
        MediaTypeHeaderValue mediatype,
        Dictionary<string, string> queryValues,
        bool IsNeedToken = false,
        CancellationToken token = default
    )
    {
        var request = new HttpRequestMessage();
        request.Method = method;
        foreach (var item in headers)
        {
            request.Headers.Add(item.Key, item.Value);
        }
        request.RequestUri = new Uri(url);
        var endcod = new FormUrlEncodedContent(queryValues);
        var query = await endcod.ReadAsStringAsync(token);
        request.Content = new StringContent(query, mediatype);
        return request;
    }

    public async Task<SignIn?> GetSignInDataAsync(KuroAccount account, GameRoilDataItem item)
    {
        var queryData = new Dictionary<string, string>()
        {
            { "gameId", item.GameId.ToString() },
            { "serverId", item.ServerId },
            { "roleId", item.RoleId },
            { "userId", item.UserId },
        };
        var header = GetDeviceHeader(account);
        var request = await BuildRequestAsync(
            "https://api.kurobbs.com/encourage/signIn/initSignInV2",
            HttpMethod.Post,
            header,
            new("application/x-www-form-urlencoded"),
            queryData,
            true
        );
        var result = await HttpClientService.HttpClient.SendAsync(request);
        var jsonStr = await result.Content.ReadAsStringAsync();
        var sign = JsonSerializer.Deserialize(jsonStr, CommunityContext.Default.SignIn);
        return sign;
    }

    public async Task<SignRecord?> GetSignRecordAsync(KuroAccount account, GameRoilDataItem item)
    {
        var header = GetDeviceHeader(account);
        var queryData = new Dictionary<string, string>()
        {
            { "gameId", item.GameId.ToString() },
            { "serverId", item.ServerId },
            { "roleId", item.RoleId },
            { "userId", item.UserId },
            { "reqMonth", DateTime.Now.Month.ToString("D2") },
        };
        var request = await BuildRequestAsync(
            "https://api.kurobbs.com/encourage/signIn/queryRecordV2",
            HttpMethod.Post,
            header,
            new("application/x-www-form-urlencoded"),
            queryData,
            true
        );
        var result = await HttpClientService.HttpClient.SendAsync(request);
        string jsonStr = await result.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize(jsonStr, CommunityContext.Default.SignRecord);
    }

    public async Task<SignInResult?> SignInAsync(
        KuroAccount account,
        GameRoilDataItem item,
        CancellationToken token = default
    )
    {
        var header = GetDeviceHeader(account);
        var queryData = new Dictionary<string, string>()
        {
            { "gameId", item.GameId.ToString() },
            { "serverId", item.ServerId },
            { "roleId", item.RoleId },
            { "userId", item.UserId },
            { "reqMonth", DateTime.Now.Month.ToString("D2") },
        };
        var request = await BuildRequestAsync(
            "https://api.kurobbs.com/encourage/signIn/v2",
            HttpMethod.Post,
            header,
            new("application/x-www-form-urlencoded"),
            queryData,
            true
        );
        var result = await HttpClientService.HttpClient.SendAsync(request);
        result.EnsureSuccessStatusCode();
        string jsonStr = await result.Content.ReadAsStringAsync();
        var jsonObj = JsonObject.Parse(jsonStr);
        if (jsonObj["code"]!.GetValue<int>() != 200) { }
        return JsonSerializer.Deserialize(jsonStr, CommunityContext.Default.SignInResult);
    }

    public async Task<AccountMine?> GetWavesMineAsync(
        KuroAccount account,
        long id,
        CancellationToken token = default
    )
    {
        var header = GetDeviceHeader(account);
        var content = new Dictionary<string, string>() { { "otherUserId", id.ToString() } };
        var request = await BuildRequestAsync(
            "https://api.kurobbs.com/user/mineV2",
            HttpMethod.Post,
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded", "utf-8"),
            content,
            true,
            token
        );
        var result = await HttpClientService.HttpClient.SendAsync(request);
        var jsonStr = await result.Content.ReadAsStringAsync();
        return (AccountMine?)
            JsonSerializer.Deserialize(jsonStr, typeof(AccountMine), CommunityContext.Default);
    }

    public async Task<bool> IsLoginAsync(KuroAccount account, CancellationToken token = default)
    {
        if (long.TryParse(account.UserId, out var result))
        {
            var mine = await GetWavesMineAsync(account, result, token);
            if (mine != null)
            {
                if (mine.Code == 200)
                    return true;
            }
        }
        return false;
    }

    public async Task<RefreshToken?> UpdateRefreshToken(
        KuroAccount account,
        GameRoilDataItem item,
        CancellationToken token = default
    )
    {
        var url = "https://api.kurobbs.com/aki/roleBox/requestToken";
        var header = new Dictionary<string, string>()
        {
            { "Accept", "application/json, text/plain, */*" },
            { "Accept-Encoding", "gzip, deflate" },
            { "Accept-Language", "zh-CN,zh;q=0.9,en-US;q=0.8,en;q=0.7" },
            {
                "devCode",
                "Mozilla/5.0 (Linux; Android 12; 23117RK66C Build/V417IR; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/101.0.4951.61 Safari/537.36 Kuro/2.5.3 KuroGameBox/2.5.3"
            },
            { "did", account.DeviceId },
            { "source", "android" },
            { "token", account.Token },
            { "Connection", "keep-alive" },
        };
        var request = await BuildRequestAsync(
            url,
            HttpMethod.Post,
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            new Dictionary<string, string>()
            {
                { "roleId", item.RoleId.ToString() },
                { "serverId", item.ServerId },
                { "userId", item.UserId.ToString() },
            },
            true,
            token
        );
        var result = await HttpClientService.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);

        var resultCode = JsonSerializer.Deserialize(
            jsonStr,
            CommunityContext.Default.GamerBassString
        );
        if (resultCode == null || resultCode.Data == null)
        {
            return null;
        }

        var bassData = JsonSerializer.Deserialize(
            resultCode.Data,
            AccessTokenContext.Default.RefreshToken
        );
        return bassData;
    }

    public async Task<ScanScreenModel?> PostQrValueAsync(
        KuroAccount account,
        string qrText,
        CancellationToken token = default
    )
    {
        var url = "https://api.kurobbs.com/user/auth/roleInfos";
        var request = await BuildRequestAsync(
            url,
            HttpMethod.Post,
            GetDeviceHeader(account),
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            new Dictionary<string, string>() { { "qrCode", qrText } },
            true
        );
        var result = await HttpClientService.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        return JsonSerializer.Deserialize<ScanScreenModel>(
            jsonStr,
            QRContext.Default.ScanScreenModel
        );
    }

    public async Task<QRLoginResult?> QRLoginAsync(
        KuroAccount account,
        string qrText,
        string verifyCode,
        string id,
        CancellationToken token = default
    )
    {
        var url = "https://api.kurobbs.com/user/auth/scanLogin";
        var request = await BuildRequestAsync(
            url,
            HttpMethod.Post,
            GetDeviceHeader(account),
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            new Dictionary<string, string>()
            {
                { "autoLogin", "true" },
                { "qrCode", qrText },
                { "id", id },
                { "verifyCode", verifyCode },
            },
            true
        );
        var result = await HttpClientService.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        return JsonSerializer.Deserialize<QRLoginResult>(jsonStr, QRContext.Default.QRLoginResult);
    }

    public async Task<SMSModel?> GetQrCodeAsync(
        KuroAccount account,
        string qrCode,
        CancellationToken token = default
    )
    {
        var query = new Dictionary<string, string>() { { "geeTestData", "" } };
        var request = await BuildLoginRequest(
            "https://api.kurobbs.com/user/sms/scanSms",
            GetDeviceHeader(account),
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            query
        );
        var result = await this.HttpClientService.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        return (SMSModel?)JsonSerializer.Deserialize(jsonStr, QRContext.Default.SMSModel);
    }

    public async Task<DeviceInfo?> GetDeviceInfosAsync(KuroAccount account, CancellationToken token = default)
    {
        var url = "https://api.kurobbs.com/user/auth/device/list";
        var request = await BuildLoginRequest(
            url,
            GetDeviceHeader(account),
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            []
        );
        var result = await this.HttpClientService.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        return (DeviceInfo?)JsonSerializer.Deserialize(jsonStr, QRContext.Default.DeviceInfo);
    }

    public async Task<SendGameVerifyCode?> SendVerifyGameCode(
        KuroAccount account,
        string gameId,
        string serverId,
        string roleId,
        CancellationToken token = default
    )
    {
        var url = "https://api.kurobbs.com/user/role/sendVerifyCode";
        var request = await BuildLoginRequest(
            url,
            GetDeviceHeader(account),
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            new Dictionary<string, string>()
            {
                { "gameId", gameId },
                { "roleId", roleId },
                { "serverId", serverId },
            }
        );
        var result = await this.HttpClientService.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        return JsonSerializer.Deserialize(jsonStr, BindGameContext.Default.SendGameVerifyCode);
    }

    public async Task<AddUserGameServer?> GetBindServerAsync(
        KuroAccount account,
        int gameId,
        CancellationToken token = default
    )
    {
        var url = "https://api.kurobbs.com/config/findGameServerList";
        var request = await BuildLoginRequest(
            url,
            GetDeviceHeader(account),
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            new Dictionary<string, string>() { { "gameId", gameId.ToString() } }
        );
        var result = await this.HttpClientService.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        return JsonSerializer.Deserialize(jsonStr, BindGameContext.Default.AddUserGameServer);
    }

    public async Task<BindGameVerifyCode?> BindGamer(
        KuroAccount account,
        string gameId,
        string serverId,
        string roleId,
        string verifyCode,
        CancellationToken token = default
    )
    {
        var url = "https://api.kurobbs.com/user/role/bindUserRole";
        var request = await BuildLoginRequest(
            url,
            GetDeviceHeader(account),
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            new Dictionary<string, string>()
            {
                { "gameId", gameId },
                { "roleId", roleId },
                { "verifyCode", verifyCode },
                { "serverId", serverId },
            }
        );
        var result = await this.HttpClientService.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        return JsonSerializer.Deserialize(jsonStr, BindGameContext.Default.BindGameVerifyCode);
    }

    public async Task InitAsync()
    {
        using (HttpClient client = new HttpClient())
        {
            this.Ip = await client.GetStringAsync("https://event.kurobbs.com/event/ip");
        }
    }

#if false
    public async Task SetAutoUserAsync(CancellationToken token = default)
    {
        try
        {
            var users = await AccountService.GetUsersAsync();
            var tokenId = await AccountService.AppSettings.GetLastSelectUserAsync().ConfigureAwait(false);
            var defaultSenect = users.FirstOrDefault(x => x.TokenId == tokenId);
            if (tokenId != null && defaultSenect != null)
            {
                var mine = await GetWavesMineAsync(
                    long.Parse(defaultSenect.TokenId),
                    defaultSenect.TokenDid,
                    defaultSenect.Token,
                    token
                );
                if (mine == null || mine.Success == false || mine.Code != 200)
                {
                    await SetAutoUserAsync(users, token);
                }
                else
                {
                    //有信息则选定这个用户
                    AccountService.SetCurrentUser(defaultSenect);
                    await AccountService.AppSettings.SetLastSelectUserAsync(defaultSenect.TokenId).ConfigureAwait(false);
                }

            }
            else
            {
                await SetAutoUserAsync(users, token);
            }
        }
        catch (Exception ex)
        {
            LoggerService.WriteError(ex.Message + ex.StackTrace);
        }
    }

    async Task SetAutoUserAsync(List<LocalAccount> accounts, CancellationToken token = default)
    {
        if (accounts.Count == 0)
        {
            return;
        }
        foreach (var item in accounts)
        {
            var mine = await GetWavesMineAsync(
                    long.Parse(item.TokenId),
                    item.TokenDid,
                    item.Token,
                    token
                );
            if (mine == null || mine.Success == false || mine.Code != 200)
            {
                await AccountService.DeleteUserAsync(item.TokenId);
                continue;
            }
            //有信息则选定这个用户
            AccountService.SetCurrentUser(item);
            await AccountService.AppSettings.SetLastSelectUserAsync(item.TokenId).ConfigureAwait(false);
        }
    }
#endif
}
