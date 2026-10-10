namespace Haiyu.Common;

public class CDNSpeedTester : IDisposable
{
    public HttpClient _client;
    private bool _disposed;

    public CDNSpeedTester(HttpMessageHandler? handler = null)
    {
        _client = handler != null ? new HttpClient(handler) : BuildDefaultClient();
    }

    private static HttpClient BuildDefaultClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.None,
            UseProxy = true,
        };

        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "identity");
        return client;
    }

    public async Task<CdnTestResult> TestAsync(
        GameResourceCdn config,
        string url,
        TimeSpan sampleDuration,
        long maxBytes = 2 * 1024 * 1024,
        CancellationToken cancellationToken = default
    )
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (sampleDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(sampleDuration));
        if (maxBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(sampleDuration);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Range", $"bytes=0-{maxBytes - 1}");
        var sw = Stopwatch.StartNew();
        long totalBytes = 0;
        Exception? error = null;
        HttpResponseMessage? response = null;

        try
        {
            response = await _client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using Stream stream = await response
                .Content.ReadAsStreamAsync(linkedCts.Token)
                .ConfigureAwait(false);
            var buffer = new byte[64 * 1024];
            while (totalBytes < maxBytes && !linkedCts.IsCancellationRequested)
            {
                int read = await stream
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), linkedCts.Token)
                    .ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }
                totalBytes += read;
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            response?.Dispose();
            sw.Stop();
        }

        long elapsed = Math.Max(1, sw.ElapsedMilliseconds);
        double speed = totalBytes * 1000.0 / elapsed;
        double score = speed * config.SpeedWeight - config.Priority * config.PriorityWeight;

        return new CdnTestResult(
            config.Url,
            error == null,
            totalBytes,
            elapsed,
            score,
            speed,
            error
        );
    }

    /// <summary>协议无关的完整 URL 候选测速。失败时下载器仍可重试全部候选。</summary>
    public async Task<string?> SelectResourceUrlAsync(IEnumerable<string> urls, TimeSpan duration, CancellationToken token = default, IEnumerable<GameResourceCdn>? cdns = null)
    {
        var results = new List<(string Url, CdnTestResult Result)>();
        foreach (var url in urls.Distinct())
        {
            token.ThrowIfCancellationRequested();
            var cdn = cdns?.FirstOrDefault(x => url.StartsWith(x.Url.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
            var result = await TestAsync(new GameResourceCdn { Url = url, SpeedWeight = cdn?.SpeedWeight ?? 1,
                PriorityWeight = cdn?.PriorityWeight ?? 0, Priority = cdn?.Priority ?? 0 }, url, duration, cancellationToken: token);
            if (result.Success && result.DownloadBytes > 0) results.Add((url, result));
        }
        token.ThrowIfCancellationRequested();
        return results.OrderByDescending(x => x.Result.Score).ThenByDescending(x => x.Result.BytesPerSecond).Select(x => x.Url).FirstOrDefault();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _client.Dispose();
    }
}

public readonly record struct CdnTestResult(
    string url,
    bool Success,
    long DownloadBytes,
    long TimeMillis,
    double Score,
    double BytesPerSecond,
    Exception? Error
)
{
    public string Url => url;

    public override string ToString()
    {
        return $"Url={Url}, Success={Success}, Bytes={DownloadBytes}, TimeMs={TimeMillis}, Bps={BytesPerSecond:F0}, Score={Score:F0}, Error={Error?.Message}";
    }
}