namespace Waves.Core.Models.Handlers;

/// <summary>
/// 游戏资源下载专用连接池。
/// </summary>
public sealed class GameDownloadSocketHandler : DelegatingHandler
{
    public GameDownloadSocketHandler()
        : base(CreateSocketHandler()) { }

    private static SocketsHttpHandler CreateSocketHandler()
    {
        return new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            MaxConnectionsPerServer = 16,
            ConnectTimeout = TimeSpan.FromSeconds(30),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            ResponseDrainTimeout = TimeSpan.FromSeconds(5),
            EnableMultipleHttp2Connections = true,
            UseCookies = false,
        };
    }
}
