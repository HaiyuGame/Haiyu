namespace Waves.Core.Services;

public class HttpClientService : IHttpClientService
{
    private readonly object _clientLock = new();

    public HttpClient HttpClient { get; private set; } = null!;
    public HttpClient GameDownloadClient { get; private set; } = null!;

    public void BuildClient()
    {
        lock (_clientLock)
        {
            // 重复初始化保持实例稳定，避免释放 Provider 和运行中请求使用的客户端。
            if (HttpClient is not null && GameDownloadClient is not null) return;

            HttpClient = new HttpClient(new WavesGameHandler());
            GameDownloadClient = new HttpClient(new GameDownloadSocketHandler());
            GameDownloadClient.DefaultRequestHeaders.ConnectionClose = false;
            GameDownloadClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "identity");
            // 流式下载由 DownloadTask 控制单次读取空闲超时。
            GameDownloadClient.Timeout = Timeout.InfiniteTimeSpan;
        }
    }
}
