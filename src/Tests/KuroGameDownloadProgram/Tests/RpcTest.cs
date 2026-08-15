using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Haiyu.RpcClient;

namespace KuroGameDownloadProgram.Tests;

public static class RpcTest
{
    public static async Task SendPingAsync()
    {
        WebSocketRpcClient client = new();
        await client.InitAsync("10010", "123456");
        await client.StartAsync(default);
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Console.WriteLine("RPC request sending...");
        var result = await client.SendRpcRequestAsync(
            new Waves.Api.Models.Rpc.RpcRequest()
            {
                RequestId = 123456,
                Method = "app_ping",
                Params = [],
            },
            timeoutCts.Token
        );
        Console.WriteLine($"RPC response: {result}");
    }
}
