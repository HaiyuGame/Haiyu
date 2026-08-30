using System.Collections.Concurrent;
using System.IO.Pipes;
using ABI.Models;
using ABIRuntime.Abstractions;
using MemoryPack;

namespace ABIRuntime.Runtime;

/// <summary>控制管道只负责调度，每个业务调用使用独立的双向管道。</summary>
public static class ElevatedHost
{
    public static async Task<int> RunAsync(string controlPipeName, string secret,
        PrivilegedServiceRegistry services)
        => await RunCoreAsync(controlPipeName, secret, services, true).ConfigureAwait(false);

    internal static Task<int> RunForTestAsync(string controlPipeName, string secret,
        PrivilegedServiceRegistry services) =>
        RunCoreAsync(controlPipeName, secret, services, false);

    private static async Task<int> RunCoreAsync(string controlPipeName, string secret,
        PrivilegedServiceRegistry services, bool requireElevation)
    {
        ArgumentNullException.ThrowIfNull(services);
        ABIMemoryPack.EnsureFormatters();
        if (requireElevation && !TokenHelper.IsElevated) return 5;

        await using var controlPipe = new NamedPipeClientStream(".", controlPipeName,
            PipeDirection.InOut, PipeOptions.Asynchronous);
        using var connectionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await controlPipe.ConnectAsync(connectionTimeout.Token).ConfigureAwait(false);
        await PipeProtocol.WriteAsync(controlPipe,
            new PipeMessage(PipeMessageKind.Request, Guid.Empty,
                PipeProtocolVersion.Current, OperationNames.Handshake,
                Convert.FromHexString(secret)),
            connectionTimeout.Token).ConfigureAwait(false);

        var activeRequests = new ConcurrentDictionary<Guid, Task>();
        using var hostShutdown = new CancellationTokenSource();

        try
        {
            while (controlPipe.IsConnected)
            {
                PipeMessage controlMessage;
                try
                {
                    controlMessage = await PipeProtocol.ReadAsync(
                        controlPipe, CancellationToken.None).ConfigureAwait(false);
                }
                catch (EndOfStreamException)
                {
                    break;
                }

                if (controlMessage.Version != PipeProtocolVersion.Current ||
                    controlMessage.Operation != OperationNames.OpenRequest)
                {
                    continue;
                }

                OpenRequestMessage? openRequest;
                try
                {
                    openRequest = MemoryPackSerializer.Deserialize<OpenRequestMessage>(
                        controlMessage.Payload, ABIMemoryPack.Options);
                }
                catch (Exception)
                {
                    continue;
                }
                if (openRequest is null || string.IsNullOrWhiteSpace(openRequest.PipeName) ||
                    string.IsNullOrWhiteSpace(openRequest.Secret))
                {
                    continue;
                }

                Guid requestId = controlMessage.RequestId;
                Task requestTask = HandleRequestPipeAsync(
                    requestId, openRequest, services, hostShutdown.Token);
                activeRequests[requestId] = requestTask;
                _ = requestTask.ContinueWith(
                    completedTask => activeRequests.TryRemove(requestId, out Task? removedTask),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
        finally
        {
            hostShutdown.Cancel();
            Task[] remaining = activeRequests.Values.ToArray();
            if (remaining.Length > 0)
            {
                try
                {
                    await Task.WhenAll(remaining).ConfigureAwait(false);
                }
                catch
                {
                    // 单次调用故障已在各自的请求管道内隔离。
                }
            }
        }

        return 0;
    }

    /// <summary>独立请求调用链：连接、握手、执行、进度/结果、关闭。</summary>
    private static async Task HandleRequestPipeAsync(Guid requestId,
        OpenRequestMessage openRequest, PrivilegedServiceRegistry services,
        CancellationToken hostShutdownToken)
    {
        await using var pipe = new NamedPipeClientStream(".", openRequest.PipeName,
            PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(hostShutdownToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        PipeMessage? request = null;

        try
        {
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            await PipeProtocol.WriteAsync(pipe,
                new PipeMessage(PipeMessageKind.Request, requestId,
                    PipeProtocolVersion.Current, OperationNames.RequestHandshake,
                    Convert.FromHexString(openRequest.Secret)), timeout.Token)
                .ConfigureAwait(false);

            request = await PipeProtocol.ReadAsync(pipe, timeout.Token).ConfigureAwait(false);
            if (request.RequestId != requestId ||
                request.Version != PipeProtocolVersion.Current)
            {
                throw new InvalidDataException("请求管道握手信息不匹配。");
            }

            if (!services.TryGet(request.Operation, out IServiceInvoker service))
            {
                await SendErrorAsync(pipe, request, unchecked((int)0x80004001),
                    $"没有注册 Service：{request.Operation}").ConfigureAwait(false);
                return;
            }

            using var remoteCancellation = new CancellationTokenSource();
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                remoteCancellation.Token, hostShutdownToken);
            using var monitorStop = CancellationTokenSource.CreateLinkedTokenSource(
                hostShutdownToken);

            Task cancellationMonitor = MonitorCancellationAsync(
                pipe, request, remoteCancellation, monitorStop.Token);
            try
            {
                await service.InvokeAsync(pipe, request, linkedCancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
            {
                if (pipe.IsConnected)
                {
                    await PipeProtocol.WriteAsync(pipe,
                        new PipeMessage(PipeMessageKind.Cancelled, request.RequestId,
                            request.Version, request.Operation, Array.Empty<byte>(),
                            0, "操作已取消"), CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                monitorStop.Cancel();
                try
                {
                    await cancellationMonitor.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (monitorStop.IsCancellationRequested)
                {
                }
            }
        }
        catch (OperationCanceledException) when (hostShutdownToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!pipe.IsConnected) return;
            PipeMessage failedRequest = request ?? new PipeMessage(
                PipeMessageKind.Request, requestId,
                PipeProtocolVersion.Current, string.Empty, Array.Empty<byte>());
            try
            {
                await SendErrorAsync(pipe, failedRequest,
                    exception.HResult, exception.ToString()).ConfigureAwait(false);
            }
            catch
            {
                // 当前请求管道失效不影响宿主及其他请求。
            }
        }
    }

    private static async Task MonitorCancellationAsync(Stream pipe, PipeMessage request,
        CancellationTokenSource remoteCancellation, CancellationToken monitorToken)
    {
        try
        {
            while (!monitorToken.IsCancellationRequested)
            {
                PipeMessage message = await PipeProtocol.ReadAsync(pipe, monitorToken)
                    .ConfigureAwait(false);
                if (message.Kind == PipeMessageKind.Cancel &&
                    message.RequestId == request.RequestId &&
                    message.Version == request.Version &&
                    message.Operation == request.Operation)
                {
                    remoteCancellation.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (monitorToken.IsCancellationRequested)
        {
        }
        catch (EndOfStreamException)
        {
            remoteCancellation.Cancel();
        }
    }

    private static ValueTask SendErrorAsync(Stream pipe, PipeMessage request,
        int statusCode, string message) =>
        PipeProtocol.WriteAsync(pipe,
            new PipeMessage(PipeMessageKind.Error, request.RequestId, request.Version,
                request.Operation, Array.Empty<byte>(), 0, message, statusCode),
            CancellationToken.None);
}
