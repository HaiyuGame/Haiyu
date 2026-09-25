namespace Waves.Core.Services;

public class HttpClientService : IHttpClientService
{
    public HttpClientService()
    {
    }

    public HttpClient HttpClient { get; private set; }

    public HttpClient GameDownloadClient { get; private set; }

    public void BuildClient()
    {
        this.HttpClient?.Dispose();
        this.GameDownloadClient?.Dispose();

        this.HttpClient = new HttpClient(new WavesGameHandler());
        this.GameDownloadClient = new HttpClient(new GameDownloadSocketHandler());
        GameDownloadClient.DefaultRequestHeaders.ConnectionClose = false;
        GameDownloadClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "identity");
        // 流式下载由 DownloadTask 控制单次读取空闲超时。
        GameDownloadClient.Timeout = Timeout.InfiniteTimeSpan;
    }
}
