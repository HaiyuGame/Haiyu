using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Haiyu.RpcClient;

public class WebSocketRpcClient : IRpcClient, IHostedService, IDisposable
{
    private ClientWebSocket? _webSocket;
    // Use 'localhost' to match HttpListener prefix (was 127.0.0.1 which can cause HttpListener to reject the request)
    private string _host = "localhost";
    private string _webSocketUrl = string.Empty;
    private bool _disposed;
    private const int ReceiveBufferSize = 4096;
    public bool EnableServerPush { get; set; } = true;

    public WebSocketRpcClient()
    {

    }

    public WebSocketRpcClient(string host)
    {
        _host = !string.IsNullOrWhiteSpace(host) ? host : _host;
    }

    public string Port { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;

    /// <summary>
    /// 初始化RPC客户端（赋值端口、Token，拼接WebSocket地址）
    /// </summary>
    /// <param name="port">服务端口</param>
    /// <param name="token">认证Token</param>
    /// <returns>初始化是否成功</returns>
    public async Task<bool> InitAsync(string port, string token)
    {
        if (string.IsNullOrWhiteSpace(port))
            throw new ArgumentNullException(nameof(port), "端口不能为空");
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentNullException(nameof(token), "认证Token不能为空");

        Port = port;
        Token = token;

        // Ensure the path matches the server prefix exactly (server listens on "/rpc/")
        _webSocketUrl = $"ws://{_host}:{Port}/rpc/";

        _webSocket = new ClientWebSocket();
        // Do not add a default sub-protocol or headers here; only add if server requires them.

        return await Task.FromResult(true);
    }

    /// <summary>
    /// 启动服务（建立WebSocket连接）- 实现IHostedService
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_webSocket == null || string.IsNullOrWhiteSpace(_webSocketUrl))
            throw new InvalidOperationException("请先调用InitAsync方法初始化客户端");

        try
        {
            await _webSocket.ConnectAsync(new Uri(_webSocketUrl), cancellationToken);
            Console.WriteLine("WebSocket连接建立成功！");

            if (EnableServerPush)
            {
                _ = Task.Run(() => ListenServerMessageAsync(cancellationToken), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket连接失败：{ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 停止服务（关闭WebSocket连接）- 实现IHostedService
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_webSocket != null)
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "客户端正常关闭", cancellationToken);
                Console.WriteLine("WebSocket连接正常关闭");
            }
            _webSocket.Dispose();
            _webSocket = null;
        }
    }

    /// <summary>
    /// 发送RPC请求并接收响应
    /// </summary>
    /// <typeparam name="TRequest">请求对象类型</typeparam>
    /// <typeparam name="TResponse">响应对象类型</typeparam>
    /// <param name="request">请求对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>RPC响应结果</returns>
    public async Task<TResponse> SendRpcRequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket未连接或连接已关闭");
        if (request == null)
            throw new ArgumentNullException(nameof(request), "RPC请求对象不能为空");

        try
        {
            var requestJson = JsonSerializer.Serialize(request);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);

            await _webSocket.SendAsync(
                new ArraySegment<byte>(requestBytes),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken);


            var responseBytes = new List<byte>();
            var buffer = new byte[ReceiveBufferSize];

            WebSocketReceiveResult? receiveResult = null;
            do
            {
                receiveResult = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);

                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await StopAsync(cancellationToken);
                    throw new InvalidOperationException("服务端已关闭WebSocket连接");
                }

                var receivedBytes = new byte[receiveResult.Count];
                Array.Copy(buffer, receivedBytes, receiveResult.Count);
                responseBytes.AddRange(receivedBytes);

            } while (!receiveResult.EndOfMessage);

            var responseJson = Encoding.UTF8.GetString(responseBytes.ToArray());
            var response = JsonSerializer.Deserialize<TResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });


            return response ?? throw new InvalidOperationException("RPC响应反序列化结果为空");
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// 后台监听服务端主动推送的消息（可选功能）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    private async Task ListenServerMessageAsync(CancellationToken cancellationToken)
    {
        if (_webSocket == null)
            return;

        var buffer = new byte[ReceiveBufferSize];
        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                var receiveResult = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);

                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await StopAsync(cancellationToken);
                    break;
                }

                var pushMessageBytes = new byte[receiveResult.Count];
                Array.Copy(buffer, pushMessageBytes, receiveResult.Count);
                var pushMessage = Encoding.UTF8.GetString(pushMessageBytes);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {

            }
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _webSocket?.Dispose();
            _webSocket = null;
        }

        _disposed = true;
    }

    ~WebSocketRpcClient()
    {
        Dispose(disposing: false);
    }
}