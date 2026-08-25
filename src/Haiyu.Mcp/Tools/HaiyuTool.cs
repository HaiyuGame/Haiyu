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
    [Description("List every KuroClient operation exposed by the local Haiyu application.")]
    public Task<string> HaiyuKuroMethodsAsync(CancellationToken cancellationToken = default) =>
        SendAsync("kuro_methods", [], includeToken: true, cancellationToken);

    [McpServerTool]
    [Description(
        "Call any KuroClient operation. argumentsJson is a JSON object containing the operation arguments. " +
        "Methods requiring an account use accountId from haiyu_account_list; account tokens stay inside Haiyu. Methods requiring a role use a role object. " +
        "Call haiyu_kuro_methods first to discover supported operation names."
    )]
    public Task<string> HaiyuKuroCallAsync(
        [Description("Exact KuroClient operation name, for example IsLoginAsync or GetGamerDataAsync.")]
            string operation,
        [Description("JSON object containing named operation arguments.")]
            string argumentsJson = "{}",
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("KuroClient operation is required.", nameof(operation));

        // Validate before forwarding so malformed model output fails at the MCP boundary.
        using var _ = JsonDocument.Parse(argumentsJson);
        return SendAsync(
            "kuro_call",
            [
                new RpcParams { Key = "operation", Value = operation },
                new RpcParams { Key = "arguments", Value = argumentsJson },
            ],
            includeToken: true,
            cancellationToken
        );
    }

    [McpServerTool]
    [Description("List locally logged-in Kuro Community accounts without exposing account tokens.")]
    public Task<string> HaiyuAccountListAsync(CancellationToken cancellationToken = default) =>
        SendAsync("account_list", [], includeToken: true, cancellationToken);

    [McpServerTool]
    [Description("Get the Kuro Community account currently selected in Haiyu.")]
    public Task<string> HaiyuAccountCurrentAsync(CancellationToken cancellationToken = default) =>
        SendAsync("account_current", [], includeToken: true, cancellationToken);

    [McpServerTool]
    [Description("List locally saved Cloud Wuthering Waves accounts without exposing login tokens.")]
    public Task<string> HaiyuCloudAccountListAsync(CancellationToken cancellationToken = default) =>
        SendAsync("cloud_account_list", [], includeToken: true, cancellationToken);

    [McpServerTool]
    [Description("Select a Cloud Wuthering Waves account and establish its local login session.")]
    public Task<string> HaiyuCloudAccountSelectAsync(
        [Description("Account ID returned by haiyu_cloud_account_list.")] string accountId,
        CancellationToken cancellationToken = default) =>
        SendAsync("cloud_account_select", [new RpcParams { Key = "accountId", Value = accountId }], true, cancellationToken);

    [McpServerTool]
    [Description("Get the Cloud Wuthering Waves gacha record identity for an account. accountId is optional and otherwise uses the selected account.")]
    public Task<string> HaiyuCloudGachaRecordInfoAsync(
        string? accountId = null,
        CancellationToken cancellationToken = default) =>
        SendAsync("cloud_gacha_record_info", OptionalParameter("accountId", accountId), true, cancellationToken);

    [McpServerTool]
    [Description("Fetch raw Cloud Wuthering Waves gacha records. Omit poolType to fetch every known pool (1-11), or pass one pool type.")]
    public Task<string> HaiyuCloudGachaRecordsAsync(
        string? accountId = null,
        int? poolType = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = OptionalParameter("accountId", accountId);
        if (poolType.HasValue)
            parameters.Add(new RpcParams { Key = "poolType", Value = poolType.Value.ToString() });
        return SendAsync("cloud_gacha_records", parameters, true, cancellationToken);
    }

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

    private static List<RpcParams> OptionalParameter(string key, string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : [new RpcParams { Key = key, Value = value }];
}
