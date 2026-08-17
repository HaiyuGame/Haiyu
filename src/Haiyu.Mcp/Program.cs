using Haiyu.Mcp;
using Haiyu.Mcp.Tools;
using Haiyu.RpcClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
    optional: false,
    reloadOnChange: false
);

var rpcSection = builder.Configuration.GetSection(RpcBridgeOptions.SectionName);
var rpcOptions = new RpcBridgeOptions
{
    Host = rpcSection["Host"] ?? "localhost",
    Port = rpcSection["Port"] ?? string.Empty,
    Token = rpcSection["Token"] ?? string.Empty,
    RequestTimeoutSeconds = int.TryParse(rpcSection["RequestTimeoutSeconds"], out var timeout)
        ? timeout
        : 10,
};

if (string.IsNullOrWhiteSpace(rpcOptions.Port))
    throw new InvalidOperationException("Rpc:Port must be configured.");
if (string.IsNullOrWhiteSpace(rpcOptions.Token))
    throw new InvalidOperationException("Rpc:Token must be configured.");

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<HaiyuTool>()
    .Services.AddSingleton(rpcOptions);

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<RpcBridgeOptions>();
    var client = new WebSocketRpcClient(options.Host);
    client.InitAsync(options.Port, options.Token).GetAwaiter().GetResult();
    return client;
});
builder.Services.AddHostedService(sp => sp.GetRequiredService<WebSocketRpcClient>());

await builder.Build().RunAsync();
