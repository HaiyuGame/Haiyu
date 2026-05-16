using System.Net;
using System.Text.Json;
using Waves.Api.Models;
using Waves.Api.Models.CloudGame;
using Waves.Core.Contracts;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.Models.CloudGame;

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

    public async Task<LoginResult?> LoginAsync(
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
        var model = JsonSerializer.Deserialize<LoginResult>(
            str,
            CloundContext.Default.LoginResult
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
        LoginData data,
        CancellationToken ct = default
    )
    {

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
}
