namespace Haiyu.Mcp;

public sealed class RpcBridgeOptions
{
    public const string SectionName = "Rpc";

    public string Host { get; init; } = "localhost";
    public string Port { get; init; } = "10010";
    public string Token { get; init; } = string.Empty;
    public int RequestTimeoutSeconds { get; init; } = 10;
}
