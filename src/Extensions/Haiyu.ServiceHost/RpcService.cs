using System;
using System.Buffers;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Waves.Api.Models;
using Waves.Api.Models.Rpc;
using Waves.Core.Services;
using Waves.Settings;

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
            var completedTask = await Task.WhenAny(
                    task,
                    Task.Delay(Timeout.Infinite, linkedCts.Token)
                )
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

    public static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var completedTask = await Task.WhenAny(
                    task,
                    Task.Delay(Timeout.Infinite, linkedCts.Token)
                )
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
    private readonly LoggerService _logger;
    private readonly RpcSettings _rpcSettings;
    private readonly SemaphoreSlim _connectionLimiter;
    private string _listenPrefix;
    private Task _listenLoopTask;
    private ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    private CancellationTokenSource _serviceCts;

    private const int MaxReceiveBufferSize = 1024 * 1024;

    private const int ConnectionIdleTimeoutMs = 300000;

    public HttpListener SocketServer { get; private set; }
    public Dictionary<string, Func<string, List<RpcParams>, Task<string>>> Method
    {
        get;
        private set;
    }

    public int Port { get; private set; } = 10010;

    public RpcService(LoggerService logger, RpcSettings rpcSettings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rpcSettings = rpcSettings;
        int maxConnections = 100;
        _connectionLimiter = new SemaphoreSlim(maxConnections, maxConnections);
        _serviceCts = new CancellationTokenSource();
    }

    public void RegisterMethod(
        Dictionary<string, Func<string, List<RpcParams>, Task<string>>> Methods
    )
    {
        Method = Methods;
    }

    #region IHostedService 实现
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        this.Port = await _rpcSettings.GetRpcLocalPortAsync(cancellationToken);
        _listenPrefix = $"http://localhost:{Port}/rpc/";
        _logger.WriteInfo("Starting Rpc WebSocket service...");
        await InitRpcAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.WriteInfo("Stopping Rpc WebSocket service...");
        _serviceCts.Cancel();
        await CloseRpcAsync(cancellationToken).ConfigureAwait(false);
    }
    #endregion

    #region IRpcService 实现
    public async Task InitRpcAsync(CancellationToken token = default)
    {
        if (SocketServer != null)
        {
            _logger.WriteWarning("RPC 服务终端，开始重新监听");
            await CloseRpcAsync(token).ConfigureAwait(false);
        }

        try
        {
            SocketServer = new HttpListener();
            SocketServer.Prefixes.Add(_listenPrefix);
            SocketServer.Start();
            _listenLoopTask = Task.Run(() => ListenForConnectionsAsync(token), token);
            _logger.WriteInfo($"Rpc WebSocket 开始监听端口:{this.Port}");
            _logger.WriteInfo($"Rpc WebSocket 地址:{_listenPrefix}");
        }
        catch (Exception ex)
        {
            _logger.WriteError($"RPC服务错误：{ex.Message}+{ex.StackTrace}");
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
                _logger.WriteInfo("Stopping Rpc listener...");

                if (_listenLoopTask != null && !_listenLoopTask.IsCompleted)
                {
                    try
                    {
                        await _listenLoopTask.WithCancellation(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.WriteInfo("Listen loop canceled gracefully");
                    }
                }

                SocketServer.Close();
                SocketServer = null;
                _listenLoopTask = null;
                _logger.WriteInfo("Rpc service stopped successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.WriteInfo($"Error closing Rpc WebSocket service,{ex.Message},{ex.StackTrace}");
            throw;
        }
        finally
        {
            _serviceCts.Dispose();
        }
    }
    #endregion

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
                    context = await SocketServer
                        .GetContextAsync()
                        .WithCancellation(token)
                        .ConfigureAwait(false);

                    if (!IsLocalClient(context.Request.RemoteEndPoint))
                    {
                        _logger.WriteWarning(
                            $"Rejected non-local connection from {context.Request.RemoteEndPoint}"
                        );
                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        context.Response.Close();
                        continue;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.WriteInfo("Listen loop canceled");
                    break;
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        _logger.WriteError(
                            $"Error waiting for client connections,{ex.Message},{ex.StackTrace}"
                        );
                    }
                    continue;
                }

                if (!context.Request.IsWebSocketRequest)
                {
                    _logger.WriteWarning(
                        $"Non-WebSocket request from {context.Request.RemoteEndPoint}"
                    );
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.Close();
                    continue;
                }

                if (!await _connectionLimiter.WaitAsync(1000, token).ConfigureAwait(false))
                {
                    _logger.WriteWarning(
                        $"Max connections reached, reject {context.Request.RemoteEndPoint}"
                    );
                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    context.Response.Close();
                    continue;
                }

                WebSocketContext webSocketContext;
                try
                {
                    webSocketContext = await context
                        .AcceptWebSocketAsync(subProtocol: null)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.WriteError(
                        $"Failed to accept WebSocket from {context.Request.RemoteEndPoint}"
                    );
                    _connectionLimiter.Release();
                    context.Response.Close();
                    continue;
                }

                WebSocket webSocket = webSocketContext.WebSocket;
                EndPoint remoteEndPoint = context.Request.RemoteEndPoint;

                _logger.WriteInfo(
                    $"New connection accepted: {remoteEndPoint} (Active: {_connectionLimiter.CurrentCount})"
                );

                // 核心：保留ConfigureAwait(false)解决UI卡顿，移除过度的ContinueWith
                _ = HandleSingleConnectionAsync(webSocket, remoteEndPoint, _serviceCts.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                _logger.WriteError("Critical error in listen loop");
            }
        }
    }

    private bool IsLocalClient(IPEndPoint remoteEndPoint)
    {
        if (remoteEndPoint is not IPEndPoint ipEndPoint)
        {
            _logger.WriteWarning($"Unsupported endpoint type: {remoteEndPoint.GetType().Name}");
            return false;
        }
        IPAddress clientIp = ipEndPoint.Address;
        bool isLocal = IPAddress.IsLoopback(clientIp);

        _logger.WriteInfo($"Client IP: {clientIp} (Local: {isLocal})");
        return isLocal;
    }

    private async Task HandleSingleConnectionAsync(
        WebSocket webSocket,
        EndPoint remoteEndPoint,
        CancellationToken token
    )
    {
        byte[] buffer = null;
        bool bufferRented = false;
        try
        {
            buffer = _arrayPool.Rent(MaxReceiveBufferSize);
            bufferRented = true;

            CancellationTokenSource idleTimeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(token);
            idleTimeoutCts.CancelAfter(ConnectionIdleTimeoutMs);

            while (webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await webSocket
                        .ReceiveAsync(new ArraySegment<byte>(buffer), idleTimeoutCts.Token)
                        .WithCancellation(token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException ex)
                {
                    if (idleTimeoutCts.IsCancellationRequested && !token.IsCancellationRequested)
                    {
                        _logger.WriteInfo($"Connection idle timeout: {remoteEndPoint}");
                    }
                    break;
                }
                catch (Exception ex)
                {
                    _logger.WriteInfo($"Error receiving message from {remoteEndPoint}");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.WriteInfo($"Client {remoteEndPoint} requested close");
                    await webSocket
                        .CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", token)
                        .ConfigureAwait(false);
                    break;
                }
                string requestMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _logger.WriteInfo($"Received from {remoteEndPoint}: {requestMessage}");

                string responseMessage = await ProcessRpcRequestAsync(
                        requestMessage,
                        remoteEndPoint,
                        token
                    )
                    .ConfigureAwait(false);

                if (
                    !string.IsNullOrEmpty(responseMessage)
                    && webSocket.State == WebSocketState.Open
                )
                {
                    byte[] responseBuffer = Encoding.UTF8.GetBytes(responseMessage);
                    await webSocket
                        .SendAsync(
                            new ArraySegment<byte>(responseBuffer),
                            WebSocketMessageType.Text,
                            endOfMessage: true,
                            token
                        )
                        .ConfigureAwait(false);

                    _logger.WriteInfo($"Sent to {remoteEndPoint}: {responseMessage}");
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
                    await webSocket
                        .CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Service stopped",
                            CancellationToken.None
                        )
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.WriteError($"Error closing WebSocket for {remoteEndPoint}");
                }
            }
            webSocket.Dispose();

            _logger.WriteInfo(
                $"Connection closed: {remoteEndPoint} (Active: {_connectionLimiter.CurrentCount})"
            );
        }
    }

    private async Task<string> ProcessRpcRequestAsync(
        string requestMessage,
        EndPoint remoteEndPoint,
        CancellationToken token
    )
    {
        long jsonId = 0;
        try
        {
            var jsonObj = JsonSerializer.Deserialize<RpcRequest>(
                requestMessage,
                RpcContext.Default.RpcRequest
            );

            if (jsonObj == null)
                throw new ArgumentException("Invalid RPC request");

            jsonId = jsonObj.RequestId;
            if (Method?.TryGetValue(jsonObj.Method, out var handler) == true)
            {
                var result = await handler
                    .Invoke(jsonObj.Method, jsonObj.Params)
                    .ConfigureAwait(false);

                return JsonSerializer.Serialize(
                    new RpcReponse
                    {
                        RequestId = jsonId,
                        Message = result,
                        Success = true,
                    },
                    RpcContext.Default.RpcReponse
                );
            }

            _logger.WriteWarning($"Unsupported method: {jsonObj.Method} from {remoteEndPoint}");
            return string.Empty;
        }
        catch (RpcException rpcException)
        {
            _logger.WriteWarning($"RPC error for {jsonId} from {remoteEndPoint}");
            return JsonSerializer.Serialize(
                new RpcReponse
                {
                    RequestId = jsonId,
                    Message = rpcException.Message,
                    Success = false,
                },
                RpcContext.Default.RpcReponse
            );
        }
        catch (Exception ex)
        {
            _logger.WriteError($"Processing error for {remoteEndPoint}");
            return JsonSerializer.Serialize(
                new RpcReponse
                {
                    RequestId = jsonId,
                    Message = ex.Message,
                    Success = false,
                },
                RpcContext.Default.RpcReponse
            );
        }
    }
}
