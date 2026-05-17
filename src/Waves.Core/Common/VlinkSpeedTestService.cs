using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Waves.Api.Models;
using Waves.Api.Models.CloudGame;

namespace Waves.Core.Common;

public class CloudNetworkSpeedTestService : IDisposable
{
    private readonly HttpClient _httpClient;

    private const string DefaultBaseUrl = "https://paas-sdk-config.vlinkcloud.cn";
    private const string FallbackBaseUrl = "https://paas-sdk-config-ks.vlinkcloud.cn";

    private int _nodeRefreshTimeMs = 300_000;
    private int _pingTimeoutMs = 500;
    private int _pingCount = 5;
    private int _pingMaxDownloadData = 1024;
    private int _showNoOperationTime = 20;
    private bool _speedUseCache;

    private readonly string _tenantKey;

    public CloudNetworkSpeedTestService(string tenantKey = "1853717215719854081")
    {
        _tenantKey = tenantKey;

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _httpClient.DefaultRequestHeaders.Add(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
        );
    }

    /// <summary>
    /// 测试本机对所有节点的连接状态
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<List<CloudNetworkDelayItem>> RunSpeedTestAsync(CancellationToken ct = default)
    {
        var result = new CloudNetworkSpeedTestResult();

        try
        {
            // Step 1: 获取节点列表（对应JS z() 函数）
            var pingResponse = await GetNodeListAsync(DefaultBaseUrl, ct);
            if (pingResponse == null)
            {
                pingResponse = await GetNodeListAsync(FallbackBaseUrl, ct);
            }

            if (pingResponse?.Lines == null || pingResponse.Lines.Count == 0)
            {
                result.IsNewSpeed = true;
                return [];
            }

            // Step 2: 应用服务器返回的配置（对应JS setConstantsBySpeedConf）
            ApplyConfig(pingResponse);
            result.Origin = pingResponse;

            // Step 3: 对每个节点进行WebSocket延迟测试（对应JS I() + C() 函数）
            var nodeDelays = await PingAllNodesAsync(pingResponse.Lines, ct);
            result.NodeDelays = nodeDelays;

            // Step 4: 按延迟升序排序，选最优节点
            if (nodeDelays.Count > 0)
            {
                var best = nodeDelays.OrderBy(n => n.Delay).First();
                result.BestNode = best;
            }
            return nodeDelays
                    ?.OrderBy(x => x.Delay)
                    .DistinctBy(x => x.NodeId)
                    .OrderBy(x => x.Delay)
                    .ToList()
                ?? new();
        }
        catch (Exception ex)
        {
            result.IsNewSpeed = true;
            return [];
        }
    }

    private async Task<CloudNetworkOrgin?> GetNodeListAsync(string baseUrl, CancellationToken ct)
    {
        var hashInput = _tenantKey + "H5";
        var md5Hash = ComputeMd5(hashInput);
        var url =
            $"{baseUrl}/ping/{md5Hash}.html?t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<CloudNetworkOrgin>(
                json,
                CloudGameContext.Default.CloudNetworkOrgin
            );

            return result;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    private void ApplyConfig(CloudNetworkOrgin config)
    {
        if (TryParseInt(config.NodeRefreshTime, out var refresh) && refresh > 0)
            _nodeRefreshTimeMs = refresh * 60 * 1000; // 分钟 → 毫秒

        if (TryParseInt(config.PingNum, out var pingNum) && pingNum > 0)
            _pingCount = pingNum;

        if (TryParseInt(config.PingMaxDownloadData, out var maxDl) && maxDl > 0)
            _pingMaxDownloadData = maxDl;

        if (TryParseInt(config.TimeOut, out var timeout) && timeout > 0)
            _pingTimeoutMs = timeout;

        if (TryParseInt(config.ShowNoOperationTime, out var noOp) && noOp > 0)
            _showNoOperationTime = noOp;

        _speedUseCache = config.SpeedUseCache == 1;
    }

    private async Task<List<CloudNetworkDelayItem>> PingAllNodesAsync(
        List<CloudNetworkOrginItem> nodes,
        CancellationToken ct
    )
    {
        var results = new ConcurrentBag<CloudNetworkDelayItem>();
        var failedCount = 0;

        // Parallel.ForEachAsync 默认并发度=Environment.ProcessorCount，可手动指定
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 3, // 最多3个并发WS连接
            CancellationToken = ct,
        };

        await Parallel.ForEachAsync(
            nodes,
            parallelOptions,
            async (node, token) =>
            {
                try
                {
                    var delay = await PingSingleNodeAsync(node, token);
                    if (delay.HasValue)
                    {
                        results.Add(
                            new CloudNetworkDelayItem
                            {
                                NodeId = node.NodeId,
                                NodeName = node.NodeName,
                                Addr = node.LineH5Addr,
                                Port = node.LineH5Port,
                                Type = node.Type,
                                Delay = (int)Math.Round(delay.Value),
                            }
                        );
                    }
                    else
                    {
                        Interlocked.Increment(ref failedCount);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref failedCount);
                }
            }
        );
        return results.ToList();
    }

    private async Task<double?> PingSingleNodeAsync(
        CloudNetworkOrginItem node,
        CancellationToken ct
    )
    {
        var wsUrl = $"wss://{node.LineH5Addr}/ws";
        using var ws = new ClientWebSocket();

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(_pingTimeoutMs * _pingCount + 5000));

        var rtts = new List<double>();

        try
        {
            await ws.ConnectAsync(new Uri(wsUrl), linkedCts.Token);

            for (int i = 0; i < _pingCount; i++)
            {
                if (linkedCts.Token.IsCancellationRequested)
                    break;

                var sendTimestamp = Stopwatch.GetTimestamp();

                // 发送: {"type":"ping","index":i,"ws_index":0}
                // 对应JS I(t,e) 函数
                var pingMsg = $$"""{"type":"ping","index":{{i}},"ws_index":0}""";
                var sendBytes = Encoding.UTF8.GetBytes(pingMsg);
                await ws.SendAsync(
                    new ArraySegment<byte>(sendBytes),
                    WebSocketMessageType.Text,
                    true,
                    linkedCts.Token
                );
                var buffer = new byte[4096];
                var receiveTask = ws.ReceiveAsync(new ArraySegment<byte>(buffer), linkedCts.Token);
                var timeoutTask = Task.Delay(_pingTimeoutMs, linkedCts.Token);

                var completed = await Task.WhenAny(receiveTask, timeoutTask);
                if (completed == timeoutTask)
                {
                    continue;
                }

                var recvResult = await receiveTask;
                var responseText = Encoding.UTF8.GetString(buffer, 0, recvResult.Count);

                using var doc = JsonDocument.Parse(responseText);
                if (
                    doc.RootElement.TryGetProperty("type", out var typeProp)
                    && typeProp.GetString() == "pong"
                )
                {
                    var elapsedMs = Stopwatch.GetElapsedTime(sendTimestamp).TotalMilliseconds;
                    rtts.Add(elapsedMs);
                }
            }

            // 对应JS C(t)函数: 计算平均延迟
            if (rtts.Count == 0)
                return null;

            var avgRtt = rtts.Average();
            return avgRtt;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (WebSocketException ex)
        {
            return null;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    private static string ComputeMd5(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash).ToLower();
    }

    private static bool TryParseInt(string? value, out int result)
    {
        if (string.IsNullOrEmpty(value))
        {
            result = 0;
            return false;
        }
        return int.TryParse(value, out result);
    }

    public void Dispose() => _httpClient?.Dispose();
}

public class CloudNetworkDelayItem
{
    public string NodeId { get; set; } = "";
    public string NodeName { get; set; } = "";
    public string Addr { get; set; } = "";
    public string Port { get; set; } = "443";
    public string Type { get; set; } = "x86";
    public int Delay { get; set; }
}

public class CloudNetworkSpeedTestResult
{
    public CloudNetworkOrgin? Origin { get; set; }

    public List<CloudNetworkDelayItem> NodeDelays { get; set; } = new();

    public CloudNetworkDelayItem? BestNode { get; set; }

    public bool IsNewSpeed { get; set; }
}
