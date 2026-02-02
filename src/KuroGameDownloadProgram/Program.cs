using Haiyu.RpcClient;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Waves.Api.Models;
using Waves.Api.Models.Rpc;
using Waves.Core.GameContext.Contexts;

const string defaultPort = "9084";
const string defaultToken = "123456";

var downloadFolder = args.Length > 0
    ? args[0]
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "HaiyuDownloadTest");

Directory.CreateDirectory(downloadFolder);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

var client = new WebSocketRpcClient
{
    EnableServerPush = false
};
await client.InitAsync(defaultPort, defaultToken);
await client.StartAsync(cts.Token);

_ = Task.Run(() => PollEventsAsync(client, cts.Token), cts.Token);

var contexts = await CallAsync(
    client,
    RpcMethod.GameContextList.ToRpcName(),
    null,
    cts.Token,
    GameLauncherSourceContext.Default.ListString);
var contextKey = nameof(PunishBiliBiliGameContext);
if (string.IsNullOrWhiteSpace(contextKey))
{
    Console.WriteLine("No game contexts available.");
    return;
}

var launcherSource = await CallAsync(
    client,
    RpcMethod.GameContextGetLauncherSource.ToRpcName(),
    new List<RpcParams> { new() { Key = "contextKey", Value = contextKey } },
    cts.Token,
    GameLauncherSourceContext.Default.GameLauncherSource);

if (launcherSource == null)
{
    Console.WriteLine("Failed to fetch launcher source.");
    return;
}

var startDownloadParams = new List<RpcParams>
{
    new() { Key = "contextKey", Value = contextKey },
    new() { Key = "folder", Value = downloadFolder },
    new() { Key = "sourceJson", Value = JsonSerializer.Serialize(launcherSource, GameLauncherSourceContext.Default.GameLauncherSource) }
};

await CallAsync<string>(client, RpcMethod.GameContextStartDownload.ToRpcName(), startDownloadParams, cts.Token);
Console.WriteLine($"Download started for '{contextKey}' into '{downloadFolder}'.");

Console.WriteLine("Press Ctrl+C to stop polling.");
while (!cts.IsCancellationRequested)
{
    await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
}

static async Task<T?> CallAsync<T>(
    WebSocketRpcClient client,
    string method,
    List<RpcParams>? parameters,
    CancellationToken token,
    JsonTypeInfo<T>? typeInfo = null)
{
    var request = new RpcRequest
    {
        Method = method,
        Params = parameters ?? new List<RpcParams>(),
        RequestId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };

    var response = await client.SendRpcRequestAsync<RpcRequest, RpcReponse>(request, token);
    if (!response.Success)
    {
        throw new InvalidOperationException(response.Message);
    }

    if (typeof(T) == typeof(string))
    {
        return (T)(object)response.Message;
    }

    if (typeInfo != null)
    {
        return JsonSerializer.Deserialize(response.Message, typeInfo);
    }

    return JsonSerializer.Deserialize<T>(response.Message);
}

static async Task PollEventsAsync(WebSocketRpcClient client, CancellationToken token)
{
    while (!token.IsCancellationRequested)
    {
        var request = new RpcRequest
        {
            Method = RpcMethod.BackendPollEvents.ToRpcName(),
            Params = new List<RpcParams>(),
            RequestId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var response = await client.SendRpcRequestAsync<RpcRequest, RpcReponse>(request, token);
        if (response.Success && !string.IsNullOrWhiteSpace(response.Message))
        {
            Console.WriteLine($"Event batch: {response.Message}");
        }

        await Task.Delay(TimeSpan.FromSeconds(1), token);
    }
}
