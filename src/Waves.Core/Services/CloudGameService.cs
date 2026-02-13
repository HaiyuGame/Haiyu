using System.Net.Http.Headers;
using System.Text.Json;
using Waves.Api.Models;
using Waves.Api.Models.CloudGame;
using Waves.Core.Contracts;
using Waves.Core.Helpers;

namespace Waves.Core.Services;

public class CloudGameService : ICloudGameService
{
    public CloudGameService(
        IHttpClientService httpClientService,
        CloudConfigManager cloudConfigManager
    )
    {
        HttpClientService = httpClientService;
        ConfigManager = cloudConfigManager;
        HttpClientService.BuildClient();
    }

    public IHttpClientService HttpClientService { get; }
    public CloudConfigManager ConfigManager { get; }
    public string RecordToken { get; private set; }

    public Dictionary<string, string> GetClientData()
    {
        var query = new Dictionary<string, string>
        {
            { "redirect_uri", "1" },
            { "__e__", "1" },
            { "pack_mark", "1" },
            { "projectId", "G152" },
            { "productId", "A1493" },
            { "channelId", "211" },
            { "deviceNum", HardwareIdGenerator.GenerateUniqueId() },
            { "version", "2.1.2" },
            { "sdkVersion", "2.1.2" },
            { "response_type", "code" },
            { "client_id", "vvkewnskrxxwfo0yi61cy24l" },
            { "deviceModel", "Chrome" },
            { "os", "Windows" },
            { "pkg", "com.kurogame.mingchao" },
            { "client_secret", "g9ej0i1jf3y68wchb0ncm266" },
            { "platform", "h5" }
        };
        return query;
    }

    public async Task<CloudSendSMS> GetPhoneSMSAsync(
        string phone,
        string geetestCaptchaOutput,
        string geetestPassToken,
        string geetestGenTime,
        string geetestLotNumber,
        CancellationToken token = default
    )
    {
        var query = GetClientData();
        query.Add("phone", phone);
        query.Add("geetestCaptchaOutput", geetestCaptchaOutput);
        query.Add("geetestPassToken", geetestPassToken);
        query.Add("geetestGenTime", geetestGenTime);
        query.Add("geetestLotNumber", geetestLotNumber);
        var request = BuildRequestMessage(
            "https://sdkapi.kurogame.com/sdkcom/v2/login/getPhoneCode.lg",
            HttpMethod.Post,
            query
        );
        var result = await HttpClientService.HttpClient.SendAsync(request, token);
        var str = await result.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<CloudSendSMS>(str, CloundContext.Default.CloudSendSMS);
    }

    public async Task<LoginResult> LoginAsync(
        string phone,
        string code,
        CancellationToken token = default
    )
    {
        var query = GetClientData();
        query.Add("phone", phone);
        query.Add("code", code);
        var request = BuildRequestMessage(
            "https://sdkapi.kurogame.com/sdkcom/v2/login/phoneCode.lg",
            HttpMethod.Post,
            query
        );
        var result = await HttpClientService.HttpClient.SendAsync(request, token);
        var model = await result.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<LoginResult>(model, CloundContext.Default.LoginResult);
    }

    /// <summary>
    /// 创建新会话确认登陆是否有效
    /// </summary>
    /// <param name="phoneToken"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<PhoneTokenModel> LoginPhoneTokenAsync(
        string phoneToken,
        string phone,
        CancellationToken token = default
    )
    {
        var query = GetClientData();
        query.Add("phone", phone);
        query.Add("token", phoneToken);
        var request = BuildRequestMessage(
            "https://sdkapi.kurogame.com/sdkcom/v2/login/phoneToken.lg",
            HttpMethod.Post,
            query
        );
        var result = await HttpClientService.HttpClient.SendAsync(request, token);
        var model = await result.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize(model, CloundContext.Default.PhoneTokenModel);
    }

    public async Task<AccessToken> GetAccessTokenAsync(
        string code,
        CancellationToken token = default
    )
    {
        var query = GetClientData();
        query.Add("code", code);
        query.Add("grant_type", "authorization_code");
        var request = BuildRequestMessage(
            "https://sdkapi.kurogame.com/sdkcom/v2/auth/getToken.lg",
            HttpMethod.Post,
            query
        );
        var result = await HttpClientService.HttpClient.SendAsync(request, token);
        var model = await result.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize(model, CloundContext.Default.AccessToken);
    }

    public async Task<EndLoginReponse> GetTokenAsync(PhoneTokenData data, string token)
    {
        try
        {
            var endLogin = new EndLoginRequest();
            endLogin.Token = token;
            endLogin.LoginType = 1;
            endLogin.UserId = data.Id.ToString();
            endLogin.UserName = data.Username.ToString();
            endLogin.Platform = "web-pc";
            endLogin.AppVersion = "1.0.6";
            endLogin.DeviceId = HardwareIdGenerator.GenerateUniqueId();
            HttpRequestMessage message = new HttpRequestMessage(
                HttpMethod.Post,
                "https://cloud-game-sh.aki-game.com/Login/Login"
            );
            var content = JsonSerializer.Serialize(endLogin, CloundContext.Default.EndLoginRequest);
            message.Content = new StringContent(
                content,
                new MediaTypeHeaderValue("application/json")
            );
            message.Headers.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36 Edg/139.0.0.0"
            );
            var result = await HttpClientService.HttpClient.SendAsync(message);
            var str = await result.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize(str, CloundContext.Default.EndLoginReponse);
            return model;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// 获取抽卡id
    /// </summary>
    /// <returns></returns>
    public async Task<RecordModel> GetRecordAsync(CancellationToken token = default)
    {
        HttpRequestMessage message = new HttpRequestMessage(
            HttpMethod.Get,
            "https://cloud-game-sh.aki-game.com/Message/GameRecordInfo"
        );
        message.Headers.Add(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36 Edg/139.0.0.0"
        );
        message.Headers.Add("x-token", RecordToken);
        var result = await HttpClientService.HttpClient.SendAsync(message, token);
        var str = await result.Content.ReadAsStringAsync(token);
        var model = JsonSerializer.Deserialize(str, CloundContext.Default.RecordModel);
        return model;
    }

    public async Task<PlayerReponse> GetGameRecordResource(string recordId, string userId, int poolType, CancellationToken token = default)
    {
        RecardQuery query = new RecardQuery();
        query.CardPoolId = "5c13a63f85465e9fcc0f24d6efb15083";
        query.RecordId = recordId;
        query.LanguageCode = "zh-Hans";
        query.PlayerId = userId;
        query.CardPoolType = poolType;
        query.ServerId = "76402e5b20be2c39f095a152090afddc";
        HttpRequestMessage message = new HttpRequestMessage(
            HttpMethod.Post,
            "https://gmserver-api.aki-game2.com/gacha/record/query"
        );
        var content = JsonSerializer.Serialize(query, CloundContext.Default.RecardQuery);
        message.Content = new StringContent(content, new MediaTypeHeaderValue("application/json"));
        message.Headers.Add(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36 Edg/139.0.0.0"
        );
        var result = await HttpClientService.HttpClient.SendAsync(message, token);
        var str = await result.Content.ReadAsStringAsync(token);
        return JsonSerializer.Deserialize(str, PlayerCardRecordContext.Default.PlayerReponse);
    }

    private HttpRequestMessage BuildRequestMessage(
        string v,
        HttpMethod post,
        Dictionary<string, string> values,
        CancellationToken token = default
    )
    {
        HttpRequestMessage message = new HttpRequestMessage();
        if (post == HttpMethod.Post)
        {
            message.Method = post;
            message.RequestUri = new(v);
            var endcod = new FormUrlEncodedContent(values);
            message.Content = endcod;
        }
        message.Headers.Add(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36 Edg/139.0.0.0"
        );
        message.Headers.Add("Kr-Ver", "1.9.0");
        return message;
    }

    public async Task<(bool, string)> OpenUserAsync(LoginData loginData, CancellationToken token = default)
    {
        try
        {
            var accessToken = await LoginPhoneTokenAsync(
                loginData.PhoneToken,
                loginData.Phone,
                token
            );
            if (accessToken.Code == 20001)
            {
                return (false, "登陆失效");
            }
            var getToken = await GetAccessTokenAsync(accessToken.Data.Code);

            var endLogin = await GetTokenAsync(accessToken.Data, getToken.Data.AccessToken);
            if (endLogin.Code == 305)
            {
                return (false, endLogin.Msg);
            }
            this.RecordToken = endLogin.Data.Token;
            return (true, "成功");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task GetUserInfoAsync(LoginData data)
    {
        var url = $"https://cloud-game-sh.aki-game.com/UserRegion/GetUserInfo?loginType={data.LoginType}&userId={data.Id}&token={data.AutoToken}&userName={data.Username}";
        var result = await HttpClientService.HttpClient.GetStringAsync(url);
    }
}
