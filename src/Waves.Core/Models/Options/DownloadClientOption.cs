namespace Waves.Core.Models.Options;

public class DownloadClientOption
{
    /// <summary>
    /// 最大连接数
    /// </summary>
    public int MaxConcurrentDownloads { get; set; } = 3;

    public Func<IWebProxy>? ProxyFactory { get; }

    public int MaxBufferSize { get; set; }

    public TimeSpan ConnectTimeout { get; set; }

    public TimeSpan PooledConnectionIdleTimeout { get; set; }

    public TimeSpan PooledConnectionLifetime { get; set; }

    public TimeSpan InfiniteTimeSpan { get; set; }

    public bool EnableMultipleHttp2Connections { get; } = new();



    public HttpClient Builder(HttpMessageHandler handler)
    {
        var builder = new HttpClient(handler);
        builder.DefaultRequestVersion = HttpVersion.Version11;
        builder.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        builder.Timeout = this.InfiniteTimeSpan;
        return builder;
    }
}

