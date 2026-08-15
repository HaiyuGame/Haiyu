using System.ComponentModel;
using System.Text.Json;
using Haiyu.RpcClient;
using ModelContextProtocol.Server;
using Waves.Api.Models.Rpc;

namespace Haiyu.Mcp.Tools;

public sealed class HaiyuTool
{
    private readonly WebSocketRpcClient _rpcClient;
    private readonly RpcBridgeOptions _options;

    public HaiyuTool(WebSocketRpcClient rpcClient, RpcBridgeOptions options)
    {
        _rpcClient = rpcClient;
        _options = options;
    }

    [McpServerTool]
    [Description("Check whether the local Haiyu application RPC service is responding.")]
    public Task<string> HaiyuPingAsync(CancellationToken cancellationToken = default) =>
        SendAsync("app_ping", [], includeToken: false, cancellationToken);

    [McpServerTool]
    [Description("Get the local Haiyu application, RPC, framework and SDK versions.")]
    public Task<string> HaiyuGetVersionAsync(CancellationToken cancellationToken = default) =>
        SendAsync("app_version", [], includeToken: true, cancellationToken);

    [McpServerTool]
    [Description("List all RPC method names supported by the local Haiyu application.")]
    public Task<string> HaiyuGetMethodsAsync(CancellationToken cancellationToken = default) =>
        SendAsync("app_methods", [], includeToken: false, cancellationToken);

    [McpServerTool]
    [Description("Call a supported Haiyu RPC method. Parameters must be a JSON array of key/value objects.")]
    public async Task<string> HaiyuCallRpcAsync(
        [Description("RPC method name, for example app_ping.")] string method,
        [Description("Optional JSON array such as [{\"key\":\"name\",\"value\":\"value\"}].")]
            string? parametersJson = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("RPC method is required.", nameof(method));

        var parameters = string.IsNullOrWhiteSpace(parametersJson)
            ? []
            : JsonSerializer.Deserialize(parametersJson, RpcContext.Default.ListRpcParams)
                ?? throw new ArgumentException("RPC parameters must be a JSON array of key/value objects.", nameof(parametersJson));

        return await SendAsync(method, parameters, includeToken: true, cancellationToken);
    }

    private async Task<string> SendAsync(
        string method,
        List<RpcParams> parameters,
        bool includeToken,
        CancellationToken cancellationToken
    )
    {
        if (includeToken && parameters.All(x => !string.Equals(x.Key, "token", StringComparison.OrdinalIgnoreCase)))
        {
            parameters.Add(new RpcParams { Key = "token", Value = _options.Token });
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds)));

        return await _rpcClient.SendRpcRequestAsync(
            new RpcRequest
            {
                RequestId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Method = method,
                Params = parameters,
            },
            timeoutCts.Token
        );
    }
}
