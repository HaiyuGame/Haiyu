using System.Net;
using System.Net.Http.Headers;
using Waves.Core.Models.CloudGame;

namespace Waves.Core.Services.CloudGameServices;

/// <summary>
/// 云游戏进程服务
/// </summary>
public class CloudGameSessionService
{
    private const string ApiBaseUrl = "https://cloud-game-sh.aki-game.com/";
    private const string OriginUrl = "https://mc.kurogames.com";
    private const string RefererUrl = "https://mc.kurogames.com/cloud/index.html";
    private const string WelinkTenantKey = "1853717215719854081";
    private const string WelinkGameId = "1853717365355843585600007";
    private const string WelinkClientVersion = "5.11.2.251216145523-wlweb-release";
    private const string WelinkScriptUrl = "https://mc.kurogames.com/cloud/WelinkCloudGame.5.11.2.251216145523-wlweb-release.min.js?v=1";
    private const string OfficialWelinkOsVersion = "Code/1.120.0 Chrome/142.0.7444.265 Electron/39.8.8 Safari/537.36";
    private const int DefaultBitRate = 18000;
    private const int DefaultFps = 60;
    private const int DefaultCodecType = 21;
    private const int DefaultPayType = 1;
    private const int QueuePendingCode = 1712;
    private const int DefaultDpi = 120;
    private const int MinStreamWidth = 1280;
    private const int MinStreamHeight = 720;
    private const int MaxStreamWidth = 1920;
    private const int MaxStreamHeight = 1080;


    private static readonly PingNode[] DefaultPingNodes =
    {
        new("2001010", 16),
        new("2001027", 22),
        new("2001021", 23),
        new("2001020", 27),
        new("2001028", 45)
    };

    private static HttpClient CreateHttpClient(SessionLaunchOptions options)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            UseCookies = false
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(ApiBaseUrl)
        };


        return client;
    }
}
