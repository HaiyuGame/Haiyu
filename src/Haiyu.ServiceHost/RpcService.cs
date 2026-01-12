using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Waves.Api.Models;
using Waves.Api.Models.Rpc;

namespace Haiyu.ServiceHost;

public static class TaskExtensions
{
    public static async Task<T> WithCancellation<T>(
        this Task<T> task,
        CancellationToken cancellationToken
    )
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, linkedCts.Token))
                .ConfigureAwait(false);

            if (completedTask == task)
            {
                linkedCts.Cancel();
                return await task.ConfigureAwait(false);
            }

            throw new OperationCanceledException(cancellationToken);
        }
        finally
        {
            linkedCts.Dispose();
        }
    }

    public static async Task WithCancellation(
        this Task task,
        CancellationToken cancellationToken
    )
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, linkedCts.Token))
                .ConfigureAwait(false);

            if (completedTask == task)
            {
                linkedCts.Cancel();
                await task.ConfigureAwait(false);
            }
            else
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }
        finally
        {
            linkedCts.Dispose();
        }
    }
}

public class RpcService : IHostedService
{
    private readonly ILogger<RpcService> _logger;
    private readonly SemaphoreSlim _connectionLimiter;
    private string _listenPrefix;
    private Task _listenLoopTask;
    private ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    private CancellationTokenSource _serviceCts;

    // 简化配置：保留核心优化，移除过度的超时限制
    private const int MaxReceiveBufferSize = 1024 * 1024;
    private const int ConnectionIdleTimeoutMs = 300000; // 延长空闲超时为5分钟，避免误关闭

    public HttpListener SocketServer { get; private set; }
    public Dictionary<string, Func<string, List<RpcParams>, Task<string>>> Method { get; private set; }

    public int Port => 9084;

    public RpcService(ILogger<RpcService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _listenPrefix = $"http://localhost:{Port}/rpc/";
        int maxConnections = 100;
        _connectionLimiter = new SemaphoreSlim(maxConnections, maxConnections);
        _serviceCts = new CancellationTokenSource();
    }

    public void RegisterMethod(Dictionary<string, Func<string, List<RpcParams>, Task<string>>> Methods)
    {
        Method = Methods;
    }

    #region IHostedService 实现
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Rpc WebSocket service...");
        // 修复：不链接_serviceCts，避免启动时取消令牌冲突
        await InitRpcAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Rpc WebSocket service...");
        _serviceCts.Cancel();
        await CloseRpcAsync(cancellationToken).ConfigureAwait(false);
    }
    #endregion

    #region IRpcService 实现
    public async Task InitRpcAsync(CancellationToken token = default)
    {
        if (SocketServer != null)
        {
            _logger.LogWarning("Rpc service is already running, restarting...");
            await CloseRpcAsync(token).ConfigureAwait(false);
        }

        try
        {
            SocketServer = new HttpListener();
            SocketServer.Prefixes.Add(_listenPrefix);
            SocketServer.Start();
            _listenLoopTask = Task.Run(() => ListenForConnectionsAsync(token), token);
            _logger.LogInformation($"Rpc WebSocket 开始监听端口:{this.Port}", _listenPrefix);
            _logger.LogInformation($"Rpc WebSocket 地址:{_listenPrefix}", _listenPrefix);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Rpc WebSocket service");
            await CloseRpcAsync(token).ConfigureAwait(false);
            throw;
        }
    }

    public async Task CloseRpcAsync(CancellationToken token = default)
    {
        try
        {
            if (SocketServer != null)
            {
                SocketServer.Stop();
                _logger.LogInformation("Stopping Rpc listener...");

                if (_listenLoopTask != null && !_listenLoopTask.IsCompleted)
                {
                    try
                    {
                        await _listenLoopTask.WithCancellation(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Listen loop canceled gracefully");
                    }
                }

                SocketServer.Close();
                SocketServer = null;
                _listenLoopTask = null;
                _logger.LogInformation("Rpc service stopped successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing Rpc WebSocket service");
            throw;
        }
        finally
        {
            _serviceCts.Dispose();
        }
    }
    #endregion

    public Task<bool> GetOpenConnectAsync() => throw new NotImplementedException();
    public Task SetOpenConnect(bool value) => throw new NotImplementedException();

    private async Task ListenForConnectionsAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && !_serviceCts.Token.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    // 修复：移除GetContextAsync的超时，恢复原有的无限等待（核心连接逻辑）
                    context = await SocketServer.GetContextAsync()
                        .WithCancellation(token)
                        .ConfigureAwait(false);

                    if (!IsLocalClient(context.Request.RemoteEndPoint))
                    {
                        _logger.LogWarning("Rejected non-local connection from {RemoteEndPoint}",
                            context.Request.RemoteEndPoint);
                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        context.Response.Close();
                        continue;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Listen loop canceled");
                    break;
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        _logger.LogError(ex, "Error waiting for client connections");
                    }
                    continue;
                }

                if (!context.Request.IsWebSocketRequest)
                {
                    _logger.LogWarning("Non-WebSocket request from {RemoteEndPoint}",
                        context.Request.RemoteEndPoint);
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.Close();
                    continue;
                }

                if (!await _connectionLimiter.WaitAsync(1000, token).ConfigureAwait(false))
                {
                    _logger.LogWarning("Max connections reached, reject {RemoteEndPoint}",
                        context.Request.RemoteEndPoint);
                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    context.Response.Close();
                    continue;
                }

                WebSocketContext webSocketContext;
                try
                {
                    webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to accept WebSocket from {RemoteEndPoint}",
                        context.Request.RemoteEndPoint);
                    _connectionLimiter.Release();
                    context.Response.Close();
                    continue;
                }

                WebSocket webSocket = webSocketContext.WebSocket;
                EndPoint remoteEndPoint = context.Request.RemoteEndPoint;

                _logger.LogInformation(
                    "New connection accepted: {RemoteEndPoint} (Active: {Count})",
                    remoteEndPoint, _connectionLimiter.CurrentCount);

                // 核心：保留ConfigureAwait(false)解决UI卡顿，移除过度的ContinueWith
                _ = HandleSingleConnectionAsync(webSocket, remoteEndPoint, _serviceCts.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                _logger.LogCritical(ex, "Critical error in listen loop");
            }
        }
    }

    private bool IsLocalClient(IPEndPoint remoteEndPoint)
    {
        if (remoteEndPoint is not IPEndPoint ipEndPoint)
        {
            _logger.LogWarning("Unsupported endpoint type: {EndpointType}",
                remoteEndPoint.GetType().Name);
            return false;
        }

        IPAddress clientIp = ipEndPoint.Address;
        bool isLocal = IPAddress.IsLoopback(clientIp);

        _logger.LogDebug("Client IP: {ClientIp} (Local: {IsLocal})", clientIp, isLocal);
        return isLocal;
    }

    private async Task HandleSingleConnectionAsync(
        WebSocket webSocket,
        EndPoint remoteEndPoint,
        CancellationToken token)
    {
        byte[] buffer = null;
        bool bufferRented = false;
        try
        {
            buffer = _arrayPool.Rent(MaxReceiveBufferSize);
            bufferRented = true;

            CancellationTokenSource idleTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            idleTimeoutCts.CancelAfter(ConnectionIdleTimeoutMs);

            while (webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), idleTimeoutCts.Token)
                        .WithCancellation(token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException ex)
                {
                    if (idleTimeoutCts.IsCancellationRequested && !token.IsCancellationRequested)
                    {
                        _logger.LogInformation("Connection idle timeout: {RemoteEndPoint}", remoteEndPoint);
                    }
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error receiving message from {RemoteEndPoint}", remoteEndPoint);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Client {RemoteEndPoint} requested close", remoteEndPoint);
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", token)
                        .ConfigureAwait(false);
                    break;
                }
                string requestMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _logger.LogDebug("Received from {RemoteEndPoint}: {Message}", remoteEndPoint, requestMessage);

                string responseMessage = await ProcessRpcRequestAsync(requestMessage, remoteEndPoint, token)
                    .ConfigureAwait(false);

                if (!string.IsNullOrEmpty(responseMessage) && webSocket.State == WebSocketState.Open)
                {
                    byte[] responseBuffer = Encoding.UTF8.GetBytes(responseMessage);
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(responseBuffer),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        token)
                        .ConfigureAwait(false);

                    _logger.LogDebug("Sent to {RemoteEndPoint}: {Message}", remoteEndPoint, responseMessage);
                }

                idleTimeoutCts.CancelAfter(ConnectionIdleTimeoutMs);
            }
        }
        finally
        {
            if (bufferRented)
            {
                _arrayPool.Return(buffer);
            }
            _connectionLimiter.Release();

            if (webSocket.State != WebSocketState.Closed)
            {
                try
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Service stopped",
                        CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing WebSocket for {RemoteEndPoint}", remoteEndPoint);
                }
            }
            webSocket.Dispose();

            _logger.LogInformation(
                "Connection closed: {RemoteEndPoint} (Active: {Count})",
                remoteEndPoint, _connectionLimiter.CurrentCount);
        }
    }

    private async Task<string> ProcessRpcRequestAsync(
        string requestMessage,
        EndPoint remoteEndPoint,
        CancellationToken token)
    {
        long jsonId = 0;
        try
        {
            var jsonObj = JsonSerializer.Deserialize<RpcRequest>(
                requestMessage,
                RpcContext.Default.RpcRequest);

            if (jsonObj == null)
                throw new ArgumentException("Invalid RPC request");

            jsonId = jsonObj.RequestId;
            if (Method?.TryGetValue(jsonObj.Method, out var handler) == true)
            {
                var result = await handler.Invoke(jsonObj.Method, jsonObj.Params)
                    .ConfigureAwait(false);

                return JsonSerializer.Serialize(
                    new RpcReponse
                    {
                        RequestId = jsonId,
                        Message = result,
                        Success = true,
                    },
                    RpcContext.Default.RpcReponse);
            }

            _logger.LogWarning("Unsupported method: {Method} from {RemoteEndPoint}", jsonObj.Method, remoteEndPoint);
            return string.Empty;
        }
        catch (RpcException rpcException)
        {
            _logger.LogWarning(rpcException, "RPC error for {RequestId} from {RemoteEndPoint}", jsonId, remoteEndPoint);
            return JsonSerializer.Serialize(
                new RpcReponse
                {
                    RequestId = jsonId,
                    Message = rpcException.Message,
                    Success = false,
                },
                RpcContext.Default.RpcReponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing error for {RemoteEndPoint}", remoteEndPoint);
            return JsonSerializer.Serialize(
                new RpcReponse
                {
                    RequestId = jsonId,
                    Message = ex.Message,
                    Success = false,
                },
                RpcContext.Default.RpcReponse);
        }
    }
}