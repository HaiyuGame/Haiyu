using System.Net.Http.Headers;
using System.Text.Json;
using Waves.Api.Models;
using Waves.Api.Models.CloudGame;
using Waves.Core.Contracts;
using Waves.Core.Helpers;
using Waves.Core.Models.CloudGame;

namespace Waves.Core.Services;

public class CloudGameService : ICloudGameService
{
    #region 常量定义
    public const string SDKBaseUrl = "https://sdkapi.kurogame.com/";
    public const string CloudBaseUrl = "https://cloud-game-sh.aki-game.com/";
    private const string ClientId = "vvkewnskrxxwfo0yi61cy24l";
    private const string ClientSecret = "g9ej0i1jf3y68wchb0ncm266";
    private const string ChannelId = "211";
    private const string GameId = "G152";
    private const string ProductId = "A1493";
    private const string Pkg = "com.kurogame.mingchao";
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36 Edg/139.0.0.0";
    private const string Platform = "web-pc";
    private const string AppVersion = "1.0.6";
    #endregion
    public HttpClient SdkClient { get; private set; }
    public HttpClient CloudClient { get; private set; }
    public CloudConfigManager ConfigManager { get; }
    public string RecordToken { get; private set; }

    public CloudGameService(CloudConfigManager cloudConfigManager)
    {
        ConfigManager = cloudConfigManager;
        Session = new();
        BuildClient();
    }

    private void BuildClient()
    {
        this.SdkClient = new HttpClient() { BaseAddress = new(SDKBaseUrl) };
        this.CloudClient = new HttpClient() { BaseAddress = new(CloudBaseUrl) };
    }

    public CloudGameLoginSession Session { get; private set; }

    /// <summary>
    /// 设置登录缓存
    /// </summary>
    public void SetLoginData(CloudGameLoginData data)
    {
        Session.OrginData = data;
    }

    public Dictionary<string, string> GetClientData(CloudGameLoginSession session = null)
    {
        var query = new Dictionary<string, string>
        {
            { "redirect_uri", "1" },
            { "__e__", "1" },
            { "pack_mark", "1" },
            { "projectId", GameId },
            { "productId", ProductId },
            { "channelId", ChannelId },
            { "version", "2.1.2" },
            { "sdkVersion", "2.1.2" },
            { "response_type", "code" },
            { "client_id", ClientId },
            { "deviceModel", "Chrome" },
            { "os", "Windows" },
            { "pkg", "com.kurogame.mingchao" },
            { "client_secret", ClientSecret },
            { "platform", "h5" },
        };
        if (session != null && session.OrginData != null && session.OrginData.LoginDid != null)
        {
            query.Add("deviceNum", session.OrginData.LoginDid);
        }
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
        var query = GetClientData(Session);
        query.Add("phone", phone);
        query.Add("geetestCaptchaOutput", geetestCaptchaOutput);
        query.Add("geetestPassToken", geetestPassToken);
        query.Add("geetestGenTime", geetestGenTime);
        query.Add("geetestLotNumber", geetestLotNumber);
        var request = BuildRequestMessage(
            "/sdkcom/v2/login/getPhoneCode.lg",
            HttpMethod.Post,
            query
        );
        var result = await SdkClient.SendAsync(request, token);
        var str = await result.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<CloudSendSMS>(str, CloudGameContext.Default.CloudSendSMS);
    }

    /// <summary>
    /// 获取抽卡id
    /// </summary>
    /// <returns></returns>
    public async Task<RecordModel> GetRecordAsync(CancellationToken token = default)
    {
        HttpRequestMessage message = new HttpRequestMessage(
            HttpMethod.Get,
            "/Message/GameRecordInfo"
        );
        message.Headers.Add(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36 Edg/139.0.0.0"
        );
        message.Headers.Add("x-token", RecordToken);
        var result = await CloudClient.SendAsync(message, token);
        var str = await result.Content.ReadAsStringAsync(token);
        var model = JsonSerializer.Deserialize(str, CloudGameContext.Default.RecordModel);
        return model;
    }

    public async Task<PlayerReponse> GetGameRecordResource(
        string recordId,
        string userId,
        int poolType,
        CancellationToken token = default
    )
    {
        RecardQuery query = new RecardQuery();
        query.CardPoolId = "5c13a63f85465e9fcc0f24d6efb15083";
        query.RecordId = recordId;
        query.LanguageCode = "zh-Hans";
        query.PlayerId = userId;
        query.CardPoolType = poolType;
        query.ServerId = "76402e5b20be2c39f095a152090afddc";
        HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, "/gacha/record/query");
        var content = JsonSerializer.Serialize(query, CloudGameContext.Default.RecardQuery);
        message.Content = new StringContent(content, new MediaTypeHeaderValue("application/json"));
        message.Headers.Add(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36 Edg/139.0.0.0"
        );
        var result = await CloudClient.SendAsync(message, token);
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
        HttpRequestMessage message = new HttpRequestMessage(post, v);
        if (post == HttpMethod.Post)
        {
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

    public async Task<(bool, string)> OpenUserAsync(
        CloudGameLoginData loginData,
        CancellationToken token = default
    )
    {
        //try
        //{
        //    this.SetLoginData(loginData);
        //    var accessToken = await LoginPhoneTokenAsync(
        //        loginData.PhoneToken,
        //        loginData.Phone,
        //        token
        //    );
        //    if (accessToken.Code != 0)
        //    {
        //        return (false, "登陆失效");
        //    }
        //    var getToken = await GetAccessTokenAsync(accessToken.Data.Code);

        //    var endLogin = await GetTokenAsync(accessToken.Data, getToken.Data.AccessToken);
        //    if (endLogin.Code == 305)
        //    {
        //        return (false, endLogin.Msg);
        //    }
        //    this.RecordToken = endLogin.Data.Token;
        //    return (true, "成功");
        //}
        //catch (Exception ex)
        //{
        //    return (false, ex.Message);
        //}
        return (true, "");
    }

    public async Task GetUserInfoAsync(CloudGameLoginData data)
    {
        var url =
            $"/UserRegion/GetUserInfo?loginType={data.LoginType}&userId={data.Id}&token={data.AutoToken}&userName={data.Username}";
        var result = await CloudClient.GetStringAsync(url);
    }
}
