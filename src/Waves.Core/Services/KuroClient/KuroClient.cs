using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Waves.Api.Models;
using Waves.Api.Models.Communitys;
using Waves.Api.Models.QRLogin;
using Waves.Core;
using Waves.Core.Contracts;
using Waves.Core.Helpers;
using Waves.Core.Services;
using WavesLauncher.Core.Contracts;

namespace Waves.Core.Services;

public sealed partial class KuroClient : IKuroClient
{
    public IHttpClientService HttpClientService { get; }
    public LoggerService LoggerService { get; }
    public string? BAT { get; private set; }
    public string Ip { get; private set; }

    public IKuroAccountService AccountService { get; }

    public KuroClient(
        IHttpClientService httpClientService,
        [FromKeyedServices("AppLog")] LoggerService loggerService,
        IKuroAccountService accountService
    )
    {
        HttpClientService = httpClientService;
        LoggerService = loggerService;
        AccountService = accountService;
        HttpClientService.BuildClient();
    }

    private Dictionary<string, string> GetDeviceHeader(bool isNeedToken, bool isNeedBAT = true)
    {
        var dict = new Dictionary<string, string>()
        {
            { "Accept", "application/json, text/plain, */*" },
            { "Accept-Encoding", "gzip, deflate" },
            { "Accept-Language", "zh-CN,zh;q=0.9,en-US;q=0.8,en;q=0.7" },
            { "source", "android" },
            { "devCode", AccountService.Current?.TokenDid ?? "" },
            //{ "model","23117RK66C"},
            { "version", "2.5.3" },
            { "lang", "zh-Hans" },
            { "countryCode", "CN" },
        };
        if (isNeedBAT)
        {
            if (!string.IsNullOrWhiteSpace(this.BAT))
                dict.Add("b-at", this.BAT ?? "");
        }
        if (isNeedToken)
        {
            if (AccountService.Current != null)
            {
                dict.Add("token", this.AccountService.Current.Token);
            }
        }
        return dict;
    }

    private Dictionary<string, string> GetWebHeader(bool isNeedToken, bool isNeedBAT = true)
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
            { "did", AccountService.Current?.TokenDid ?? "" },
            { "source", "android" },
            {
                "devCode",
                $"{this.Ip}, Mozilla/5.0 (Linux; Android 12; 23117RK66C Build/V417IR; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/101.0.4951.61 Safari/537.36 Kuro/2.5.3 KuroGameBox/2.5.3"
            },
        };
        if (isNeedBAT)
        {
            if (!string.IsNullOrWhiteSpace(this.BAT))
                dict.Add("b-at", this.BAT ?? "");
        }
        if (isNeedToken)
        {
            if (AccountService.Current != null)
            {
                dict.Add("token", this.AccountService.Current.Token);
            }
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

    public async Task<SignIn?> GetSignInDataAsync(GameRoilDataItem item)
    {
        var queryData = new Dictionary<string, string>()
        {
            { "gameId", item.GameId.ToString() },
            { "serverId", item.ServerId },
            { "roleId", item.RoleId },
            { "userId", item.UserId },
        };
        var header = GetDeviceHeader(true);
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

    public async Task<SignRecord?> GetSignRecordAsync(GameRoilDataItem item)
    {
        var header = GetDeviceHeader(true);
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
        GameRoilDataItem item,
        CancellationToken token = default
    )
    {
        var header = GetDeviceHeader(true, false);
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

    public async Task<AccountMine?> GetWavesMineAsync(long id, CancellationToken token = default)
    {
        var header = GetDeviceHeader(true);
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

    public async Task<AccountMine?> GetWavesMineAsync(
        long id,
        string tokenDid,
        string tokenValue,
        CancellationToken token = default
    )
    {
        var header = GetDeviceHeader(true);
        header["devCode"] = tokenDid;
        header["token"] = tokenValue;
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

    public async Task<bool> IsLoginAsync(CancellationToken token = default)
    {
        if (this.AccountService.Current == null)
        {
            return false;
        }
        if (long.TryParse(AccountService.Current.TokenId, out var result))
        {
            var mine = await GetWavesMineAsync(
                result,
                AccountService.Current.TokenDid,
                AccountService.Current.Token,
                token
            );
            if (mine != null)
            {
                if (mine.Code == 200)
                    return true;
            }
        }
        return false;
    }

    public async Task<RefreshToken?> UpdateRefreshToken(
        GameRoilDataItem item,
        CancellationToken token = default
    )
    {
        if (AccountService.Current == null)
            return null;
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
            { "did", AccountService.Current.TokenDid },
            { "source", "android" },
            { "token", AccountService.Current.Token },
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
        if (resultCode == null)
        {
            return null;
        }

        var bassData = JsonSerializer.Deserialize(
            resultCode.Data,
            AccessTokenContext.Default.RefreshToken
        );
        if (bassData != null)
        {
            this.BAT = bassData.AccessToken;
        }
        return bassData;
    }

    public async Task<ScanScreenModel?> PostQrValueAsync(
        string qrText,
        CancellationToken token = default
    )
    {
        var url = "https://api.kurobbs.com/user/auth/roleInfos";
        var request = await BuildRequestAsync(
            url,
            HttpMethod.Post,
            GetDeviceHeader(true, false),
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
            GetDeviceHeader(true, false),
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

    public async Task<SMSModel?> GetQrCodeAsync(string qrCode, CancellationToken token = default)
    {
        var query = new Dictionary<string, string>() { { "geeTestData", "" } };
        var request = await BuildLoginRequest(
            "https://api.kurobbs.com/user/sms/scanSms",
            GetDeviceHeader(true, false),
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            query
        );
        var result = await this.HttpClientService.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        return (SMSModel?)JsonSerializer.Deserialize(jsonStr, QRContext.Default.SMSModel);
    }

    public async Task<DeviceInfo?> GetDeviceInfosAsync(CancellationToken token = default)
    {
        var url = "https://api.kurobbs.com/user/auth/device/list";
        var request = await BuildLoginRequest(
            url,
            GetDeviceHeader(true, false),
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            []
        );
        var result = await this.HttpClientService.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        return (DeviceInfo?)JsonSerializer.Deserialize(jsonStr, QRContext.Default.DeviceInfo);
    }

    public async Task<SendGameVerifyCode?> SendVerifyGameCode(
        string gameId,
        string serverId,
        string roleId,
        CancellationToken token = default
    )
    {
        var url = "https://api.kurobbs.com/user/role/sendVerifyCode";
        var request = await BuildLoginRequest(
            url,
            GetDeviceHeader(true, false),
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
        int gameId,
        CancellationToken token = default
    )
    {
        var url = "https://api.kurobbs.com/config/findGameServerList";
        var request = await BuildLoginRequest(
            url,
            GetDeviceHeader(true, false),
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            new Dictionary<string, string>() { { "gameId", gameId.ToString() } }
        );
        var result = await this.HttpClientService.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        return JsonSerializer.Deserialize(jsonStr, BindGameContext.Default.AddUserGameServer);
    }

    public async Task<BindGameVerifyCode?> BindGamer(
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
            GetDeviceHeader(true, false),
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
}
