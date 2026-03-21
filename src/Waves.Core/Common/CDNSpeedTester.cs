using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using Waves.Api.Models;
using Waves.Api.Models.Communitys;
using Waves.Core.Models.Downloader;

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
        CdnList config,
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
        double score = speed * config.K1 - config.P * config.K2;

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

    public async Task<IReadOnlyList<CdnTestResult>> TestAllAsync(
        IEnumerable<CdnList> configs,
        string url,
        IndexResource resource,
        TimeSpan sampleDuration,
        long maxBytes = 2 * 1024 * 1024,
        CancellationToken cancellationToken = default
    )
    {
        if (configs == null)
            throw new ArgumentNullException(nameof(configs));

        var results = new List<CdnTestResult>();
        foreach (var cfg in configs)
        {
            string url2 = cfg.Url + url + resource.Dest;
            cancellationToken.ThrowIfCancellationRequested();
            var result = await TestAsync(cfg, url2, sampleDuration, maxBytes, cancellationToken)
                .ConfigureAwait(false);
            results.Add(result);
        }
        return results;
    }

    public async Task<CdnTestResult> TestAllAsync(
        IEnumerable<CdnList> config,
        string allUrl,
        TimeSpan duration,
        long maxBytes = 2 * 1024 * 1024,
        CancellationToken token = default
    )
    {
        if(config == null)
        {
            throw new ArgumentException(nameof(config));
        }
        var result = new List<CdnTestResult>();
        foreach (var item in config)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var url = item.Url + allUrl;
                var testResult = await TestAsync(item, url, duration, maxBytes, token)
                    .ConfigureAwait(false);
                result.Add(testResult);
            }
            catch (Exception)
            {
                continue;
            }
        }
        var best = result
                .Where(r => r.Success && r.DownloadBytes > 0)
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => r.BytesPerSecond)
                .FirstOrDefault();
        return best;
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
