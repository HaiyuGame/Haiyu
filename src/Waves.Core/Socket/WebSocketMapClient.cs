using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;

namespace Waves.Core.Socket;

public class WebSocketMapClient : IDisposable, IAsyncDisposable
{
    private ClientWebSocket? _webSocket;
    private string _webSocketUrl = string.Empty;
    private bool _disposed;
    private CancellationTokenSource cts = null;
    private PeriodicTimer headTimer;

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }

    public async Task StartAsync(string url)
    {
        if (_webSocket == null)
            _webSocket = new ClientWebSocket();
        cts = new();
        this._webSocketUrl = url;
        await _webSocket.ConnectAsync(new(url), cts.Token);
        _ = Task.Run(() => StartRunAsync());
        _ = Task.Run(() => SendPingTaskAsync());
    }

    private async Task SendPingTaskAsync()
    {
        if (_webSocket == null)
            return;
        headTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        var buffer = new byte[4096];
        try
        {
            while (await headTimer.WaitForNextTickAsync())
            {
                if(cts.IsCancellationRequested && _webSocket.State != WebSocketState.Open)
                {
                    Debug.WriteLine("库洛地图组件：WebSocket连接已关闭，停止发送心跳");
                    break;
                }
                await _webSocket.SendAsync(Encoding.UTF8.GetBytes("ping"), WebSocketMessageType.Text, true, cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { }
    }

    private async Task StopAsync(object cancellationToken)
    {
        await cts.CancelAsync().ConfigureAwait(false);
    }

    private async Task StartRunAsync()
    {
        if (_webSocket == null)
            return;
        var buffer = new byte[4096];
        try
        {
            while (!cts.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                var receiveResult = await _webSocket
                    .ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token)
                    .ConfigureAwait(false);

                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await StopAsync(cts);
                    break;
                }
                var pushMessageBytes = new byte[receiveResult.Count];
                Array.Copy(buffer, pushMessageBytes, receiveResult.Count);
                var pushMessage = Encoding.UTF8.GetString(pushMessageBytes);
                Debug.WriteLine(pushMessage);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { }
    }
}
