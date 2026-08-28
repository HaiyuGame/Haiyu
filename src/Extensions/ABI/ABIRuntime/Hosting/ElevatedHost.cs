using System.IO.Pipes;
using ABI.Models;
using ABIRuntime.Abstractions;

namespace ABIRuntime.Runtime;

public static class ElevatedHost
{
    public static async Task<int> RunAsync(string pipeName, string secret,
        PrivilegedServiceRegistry services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!TokenHelper.IsElevated) return 5;

        await using var pipe = new NamedPipeClientStream(".", pipeName,
            PipeDirection.InOut, PipeOptions.Asynchronous);
        using var connectionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await pipe.ConnectAsync(connectionTimeout.Token).ConfigureAwait(false);
        await PipeProtocol.WriteAsync(pipe,
            new PipeMessage(PipeMessageKind.Request, Guid.Empty,
                PipeProtocolVersion.Current, OperationNames.Handshake, secret),
            connectionTimeout.Token).ConfigureAwait(false);

        while (pipe.IsConnected)
        {
            PipeMessage request;
            try
            {
                request = await PipeProtocol.ReadAsync(pipe, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (EndOfStreamException)
            {
                return 0;
            }

            if (request.Version != PipeProtocolVersion.Current)
            {
                await SendErrorAsync(pipe, request, unchecked((int)0x8007000D),
                    "协议版本不兼容。").ConfigureAwait(false);
                continue;
            }

            if (!services.TryGet(request.Operation, out IServiceInvoker service))
            {
                await SendErrorAsync(pipe, request, unchecked((int)0x80004001),
                    $"没有注册 Service：{request.Operation}").ConfigureAwait(false);
                continue;
            }

            try
            {
                using var remoteCancellation = new CancellationTokenSource();
                using var requestCancellation = CancellationTokenSource
                    .CreateLinkedTokenSource(remoteCancellation.Token);
                using var monitorStop = new CancellationTokenSource();
                Task cancellationMonitor = MonitorCancellationAsync(
                    pipe, request, remoteCancellation, monitorStop.Token);

                try
                {
                    await service.InvokeAsync(pipe, request, requestCancellation.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                    when (requestCancellation.IsCancellationRequested)
                {
                    if (pipe.IsConnected)
                    {
                        await PipeProtocol.WriteAsync(pipe,
                            new PipeMessage(PipeMessageKind.Cancelled,
                                request.RequestId, request.Version, request.Operation,
                                "", 0, "操作已取消"), CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                }
                finally
                {
                    monitorStop.Cancel();
                    try
                    {
                        await cancellationMonitor.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                        when (monitorStop.IsCancellationRequested)
                    {
                    }
                }
            }
            catch (Exception exception)
            {
                await SendErrorAsync(pipe, request, exception.HResult, exception.Message)
                    .ConfigureAwait(false);
            }
        }

        return 0;
    }

    private static async Task MonitorCancellationAsync(Stream pipe,
        PipeMessage request, CancellationTokenSource remoteCancellation,
        CancellationToken monitorToken)
    {
        try
        {
            while (!monitorToken.IsCancellationRequested)
            {
                PipeMessage message = await PipeProtocol.ReadAsync(pipe, monitorToken)
                    .ConfigureAwait(false);
                if (message.Kind == PipeMessageKind.Cancel
                    && message.RequestId == request.RequestId
                    && message.Version == request.Version
                    && message.Operation == request.Operation)
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
                request.Operation, "", 0, message, statusCode), CancellationToken.None);
}
