using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Waves.Api.Models;
using Waves.Api.Models.CloudGame;
using Waves.Core.Contracts;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.Helpers;
using Waves.Core.Models.CloudGame;
using static System.Collections.Specialized.BitVector32;

namespace Waves.Core.Services.CloudGameServices;

public class WavesCloudGameService : IWavesCloudGameService
{
    public CloudConfigManager ConfigManager { get; }

    #region 配置
    private const string SdkBaseUrl = "https://sdkapi.kurogame.com/";

    private const string CloudBaseUrl = "https://cloud-game-sh.aki-game.com/";

    private const string ClientId = "vvkewnskrxxwfo0yi61cy24l";

    private const string ClientSecret = "g9ej0i1jf3y68wchb0ncm266";

    private const string ChannelId = "211";

    private const string GameId = "G152";

    private const string ProductId = "A1493";

    private const string Pkg = "com.kurogame.mingchao";
    private const string Platform = "web-pc";
    private const string AppVersion = "1.0.6";
    /// <summary>
    /// 登录请求使用的浏览器标识头。
    /// </summary>
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36 Edg/139.0.0.0";

    /// <summary>
    /// 访问 SDK 登录接口的客户端。
    /// </summary>
    private readonly HttpClient _sdkClient;

    /// <summary>
    /// 访问云游戏登录接口的客户端。
    /// </summary>
    private readonly HttpClient _cloudClient;

    private static HttpClient CreateClient(string baseUrl)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip
                | DecompressionMethods.Deflate
                | DecompressionMethods.Brotli,
            UseCookies = false,
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Add("Kr-Ver", "1.9.0");
        return client;
    }
    #endregion

    public WavesCloudGameService(CloudConfigManager cloudConfigManager)
    {
        this.ConfigManager = cloudConfigManager;
        _sdkClient = CreateClient(SdkBaseUrl);
        _cloudClient = CreateClient(CloudBaseUrl);
    }

    public async Task<Tuple<CloudSendSMS?, CloudGameLoginSnapshot>> GetPhoneSMSAsync(
        string phone,
        string geetestCaptchaOutput,
        string geetestPassToken,
        string geetestGenTime,
        string geetestLotNumber,
        CancellationToken token = default
    )
    {
        CloudGameLoginSnapshot loginSnapshot = CloudGameLoginSnapshot.Create();
        var querys = GetClientData(loginSnapshot);
        querys.Add("phone", phone);
        querys.Add("geetestCaptchaOutput", geetestCaptchaOutput);
        querys.Add("geetestPassToken", geetestPassToken);
        querys.Add("geetestGenTime", geetestGenTime);
        querys.Add("geetestLotNumber", geetestLotNumber);
        var str = await PostFormAsync(
            _sdkClient,
            "/sdkcom/v2/login/getPhoneCode.lg",
            querys,
            token
        );
        return new Tuple<CloudSendSMS?, CloudGameLoginSnapshot>(
            JsonSerializer.Deserialize<CloudSendSMS?>(str, CloundContext.Default.CloudSendSMS),
            loginSnapshot
        );
    }

    public async Task<CloudApiResponse<CloudGameLoginData>?> LoginAsync(
        CloudGameLoginSnapshot snapshot,
        string phone,
        string code,
        CancellationToken token = default
    )
    {
        var query = GetClientData(snapshot);
        query.Add("phone", phone);
        query.Add("code", code);
        var str = await PostFormAsync(
            _sdkClient,
            "sdkcom/v2/login/phoneCode.lg",
            query,
            token
        );
        var model = JsonSerializer.Deserialize(
            str,
            CloundContext.Default.CloudApiResponseCloudGameLoginData
        );
        if(model != null && model.Data!= null)
        {
            model.Data.LoginDid = snapshot.DeviceNum;
        }
        return model;
    }

    /// <summary>
    /// 检查登录是否过期
    /// </summary>
    /// <param name="data"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task AuthenticateAsync(
        CloudGameLoginData data,
        CancellationToken ct = default
    )
    {

    }

    public async Task<CloudApiResponse<PhoneTokenData>?> RefreshPhoneTokenAsync(CloudGameLoginData data, CancellationToken ct = default)
    {
        var snapshot = CloudGameLoginSnapshot.Create(data);
        var querys = GetClientData(snapshot);
        querys.Add("phone", data.Phone);
        querys.Add("token", data.PhoneToken);
        var json = await PostFormAsync(this._sdkClient, "sdkcom/v2/login/phoneToken.lg", querys, ct);
        return JsonSerializer.Deserialize(json, CloundContext.Default.CloudApiResponsePhoneTokenData);
    }

    public async Task<CloudApiResponse<AccessData>?> GetAccessToken(CloudGameLoginData data,string refreshPhoneToken, CancellationToken ct = default)
    {
        var snapshot = CloudGameLoginSnapshot.Create(data);
        var query = GetClientData(snapshot);
        query.Add("code", refreshPhoneToken);
        query.Add("grant_type", "authorization_code");
        var json = await PostFormAsync(this._sdkClient, "sdkcom/v2/auth/getToken.lg", query, ct);
        return JsonSerializer.Deserialize(json, CloundContext.Default.CloudApiResponseAccessData);
    }

    public async Task<CloudApiResponse<EndLoginReponseData>?> GetTokenAsync(CloudGameLoginData data,string accessToken,CancellationToken ct = default)
    {
        var req = new EndLoginRequest
        {
            Token = accessToken,
            LoginType = 1,
            UserId = data.Id.ToString(),
            UserName = data.Username,
            Platform = "web-pc",
            AppVersion = "1.0.6",
            DeviceId = data.LoginDid,
        };
        var json = JsonSerializer.Serialize(req, CloundContext.Default.EndLoginRequest);
        var result = await PostJsonAsync(_cloudClient,"Login/Login",json,ct);
        return JsonSerializer.Deserialize(result,CloundContext.Default.CloudApiResponseEndLoginReponseData);
    }

    public Dictionary<string, string> GetClientData(CloudGameLoginSnapshot session = null)
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
        if (session != null)
        {
            query.Add("deviceNum", session.DeviceNum);
        }
        return query;
    }

    public async Task<CloudApiResponse<bool>> FetchMesageAsync(CloudGameLoginSession session,CancellationToken ct = default)
    {
        using var client = BuildClientData(session, "Message/FetchMessage",method:HttpMethod.Get);
        var result = await _cloudClient.SendAsync(client,ct);
        var str = await result.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<CloudApiResponse<bool>>(str, CloundContext.Default.CloudApiResponseBoolean);
    }

    public HttpRequestMessage BuildClientData(CloudGameLoginSession session,string path,HttpMethod method)
    {
        HttpRequestMessage message = new(method, path);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        message.Headers.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9");
        message.Headers.Referrer = new Uri("https://mc.kurogames.com/cloud/index.html");
        message.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Code/1.120.0 Chrome/142.0.7444.265 Electron/39.8.8 Safari/537.36");
        message.Headers.TryAddWithoutValidation("Origin", "https://mc.kurogames.com");
        message.Headers.TryAddWithoutValidation("Cookie", BuildCookieHeader(session));
        message.Headers.TryAddWithoutValidation("x-os", "web");
        message.Headers.TryAddWithoutValidation("x-token", session.EndLoginData.Token);
        message.Headers.TryAddWithoutValidation("x-os", "web");
        message.Headers.TryAddWithoutValidation("x-b3-traceid", session.TraceId);
        return message;
    }

    /// <summary>
    /// 将启动参数中的 cookie 合成为 HTTP 请求头字符串。
    /// </summary>
    private static string BuildCookieHeader(CloudGameLoginSession options)
    {
        var cookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        cookies.Add("username", options.OrginData.Username);
        cookies.Add("sdkuserid", options.OrginData.Sdkuserid);
        cookies.Add("cuid", options.OrginData.Cuid);
        cookies.Add("code", options.OrginData.Code);
        if (!string.IsNullOrWhiteSpace(options.AccessData.AccessToken))
        {
            cookies.TryAdd("autoToken", options.AccessData.AccessToken);
        }

        if (!string.IsNullOrWhiteSpace(options.OrginData.PhoneToken))
        {
            cookies.TryAdd("phoneToken", options.OrginData.PhoneToken);
        }

        if (!string.IsNullOrWhiteSpace(options.EndLoginData.Token))
        {
            cookies["token"] = options.EndLoginData.Token;
        }

        if (cookies.Count == 0)
        {
        }

        return string.Join("; ", cookies.Select(pair => $"{pair.Key}={pair.Value}"));
    }


    private static async Task<string> PostFormAsync(
        HttpClient client,
        string path,
        Dictionary<string, string> values,
        CancellationToken ct
    )
    {
        using var content = new FormUrlEncodedContent(values!);
        using var response = await client.PostAsync(path, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return body;
    }

    private static async Task<string> PostJsonAsync(
        HttpClient client,
        string path,
        string payload,
        CancellationToken ct
    )
    {
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(path, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return body;
    }

}
