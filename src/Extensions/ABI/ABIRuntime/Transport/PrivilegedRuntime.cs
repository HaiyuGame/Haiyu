using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using ABI.Models;
using ABIRuntime.Abstractions;
using MemoryPack;

namespace ABIRuntime.Runtime;

/// <summary>控制管道复用；每次 InvokeAsync 使用独立的双向请求管道。</summary>
public sealed class PrivilegedRuntime : IAsyncDisposable
{
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly SemaphoreSlim _controlWriteGate = new(1, 1);
    private readonly string _coreDllPath;
    private NamedPipeServerStream? _controlPipe;
    private string? _controlSecret;
    private bool _disposed;

    public int RunFlage { get; private set; }

    public PrivilegedRuntime(string coreDllPath)
    {
        ABIMemoryPack.EnsureFormatters();
        if (string.IsNullOrWhiteSpace(coreDllPath) || !File.Exists(coreDllPath))
            throw new ArgumentException("Core DLL 路径为空或不存在。", nameof(coreDllPath));
        _coreDllPath = Path.GetFullPath(coreDllPath);
    }

    public async ValueTask<IPrivilegedResult<TResponse>> InvokeAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TRequest,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProgress>(
        PrivilegedServiceContract<TRequest, TResponse, TProgress> contract,
        TRequest request,
        IProgress<IPrivilegedProgress<TProgress>>? progress = null,
        CancellationToken cancellationToken = default)
        where TResponse : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(contract);
        progress?.Report(new PrivilegedProgress<TProgress>(
            PrivilegedStage.Preparing, 0, "正在准备请求"));

        try
        {
            TResponse response = await InvokeCoreAsync(
                contract, request, progress, cancellationToken).ConfigureAwait(false);
            return new PrivilegedResult<TResponse>(true, 0, "操作成功", response);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new PrivilegedResult<TResponse>(
                false, exception.HResult, exception.Message, null);
        }
    }

    private async ValueTask<TResponse> InvokeCoreAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TRequest,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProgress>(
        PrivilegedServiceContract<TRequest, TResponse, TProgress> contract,
        TRequest request,
        IProgress<IPrivilegedProgress<TProgress>>? progress,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        NamedPipeServerStream controlPipe = await EnsureControlPipeAsync(
            progress, cancellationToken).ConfigureAwait(false);

        Guid requestId = Guid.NewGuid();
        string requestPipeName = $"ABIRuntime.Request.{Convert.ToHexString(
            RandomNumberGenerator.GetBytes(24))}";
        string requestSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using NamedPipeServerStream requestPipe = CreateSecuredPipe(requestPipeName);

        byte[] openPayload = MemoryPackSerializer.Serialize(
            new OpenRequestMessage(requestPipeName, requestSecret), ABIMemoryPack.Options);

        await _controlWriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await PipeProtocol.WriteAsync(
                controlPipe,
                new PipeMessage(
                    PipeMessageKind.Request,
                    requestId,
                    PipeProtocolVersion.Current,
                    OperationNames.OpenRequest,
                    openPayload),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            InvalidateControlPipe();
            throw;
        }
        finally
        {
            _controlWriteGate.Release();
        }

        progress?.Report(new PrivilegedProgress<TProgress>(
            PrivilegedStage.Connecting, 35, "正在连接独立请求管道"));

        using var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        connectionTimeout.CancelAfter(TimeSpan.FromSeconds(30));
        await requestPipe.WaitForConnectionAsync(connectionTimeout.Token).ConfigureAwait(false);

        PipeMessage handshake = await PipeProtocol.ReadAsync(
            requestPipe, connectionTimeout.Token).ConfigureAwait(false);
        bool validHandshake =
            handshake.RequestId == requestId &&
            handshake.Version == PipeProtocolVersion.Current &&
            handshake.Operation == OperationNames.RequestHandshake &&
            CryptographicOperations.FixedTimeEquals(
                handshake.Payload,
                Convert.FromHexString(requestSecret));
        if (!validHandshake)
            throw new InvalidDataException("独立请求管道握手失败。");

        byte[] requestPayload = MemoryPackSerializer.Serialize(request, ABIMemoryPack.Options);
        progress?.Report(new PrivilegedProgress<TProgress>(
            PrivilegedStage.Executing, 40, "正在向高权限宿主发送业务请求"));

        await PipeProtocol.WriteAsync(
            requestPipe,
            new PipeMessage(
                PipeMessageKind.Request,
                requestId,
                PipeProtocolVersion.Current,
                contract.Operation,
                requestPayload),
            cancellationToken).ConfigureAwait(false);

        progress?.Report(new PrivilegedProgress<TProgress>(
            PrivilegedStage.Executing, 45, "业务请求已发送，正在等待宿主执行"));

        bool cancellationSent = false;
        using var cancellationSignal = new CancellationTokenSignal(cancellationToken);
        using var cancellationGrace = new CancellationTokenSource();

        while (true)
        {
            Task<PipeMessage> readTask = PipeProtocol.ReadAsync(
                requestPipe, cancellationGrace.Token).AsTask();

            if (!cancellationSent)
            {
                Task completed = await Task.WhenAny(readTask, cancellationSignal.Task)
                    .ConfigureAwait(false);
                if (completed == cancellationSignal.Task)
                {
                    cancellationSent = true;
                    using var sendTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    try
                    {
                        await PipeProtocol.WriteAsync(
                            requestPipe,
                            new PipeMessage(
                                PipeMessageKind.Cancel,
                                requestId,
                                PipeProtocolVersion.Current,
                                contract.Operation,
                                Array.Empty<byte>()),
                            sendTimeout.Token).ConfigureAwait(false);
                    }
                    catch when (!readTask.IsCompleted)
                    {
                        throw new OperationCanceledException(
                            "发送远端取消消息时请求管道已关闭。", cancellationToken);
                    }
                    cancellationGrace.CancelAfter(TimeSpan.FromSeconds(5));
                }
            }

            PipeMessage message;
            try
            {
                message = await readTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationSent)
            {
                throw new OperationCanceledException(
                    "高权限 Service 未在取消宽限时间内结束。", cancellationToken);
            }

            if (message.RequestId != requestId ||
                message.Version != PipeProtocolVersion.Current ||
                message.Operation != contract.Operation)
            {
                throw new InvalidDataException("响应与当前 Service 请求不匹配。");
            }

            if (message.Kind == PipeMessageKind.Progress)
            {
                TProgress? data = message.Payload.Length == 0
                    ? default
                    : MemoryPackSerializer.Deserialize<TProgress>(
                        message.Payload, ABIMemoryPack.Options);
                progress?.Report(new PrivilegedProgress<TProgress>(
                    PrivilegedStage.Executing,
                    message.Percentage,
                    message.Message,
                    data));
                continue;
            }

            if (message.Kind == PipeMessageKind.Result)
            {
                TResponse response = MemoryPackSerializer.Deserialize<TResponse>(
                    message.Payload, ABIMemoryPack.Options)
                    ?? throw new InvalidDataException("Service 响应为空。");
                progress?.Report(new PrivilegedProgress<TProgress>(
                    PrivilegedStage.Completed, 100, message.Message));
                return response;
            }

            if (message.Kind == PipeMessageKind.Cancelled)
                throw new OperationCanceledException(message.Message, cancellationToken);

            throw new InvalidOperationException(
                $"高权限 Service 失败：0x{message.StatusCode:X8} {message.Message}");
        }
    }

    private async ValueTask<NamedPipeServerStream> EnsureControlPipeAsync<TProgress>(
        IProgress<IPrivilegedProgress<TProgress>>? progress,
        CancellationToken cancellationToken)
    {
        if (_controlPipe is { IsConnected: true })
            return _controlPipe;

        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_controlPipe is { IsConnected: true })
                return _controlPipe;

            InvalidateControlPipe();
            string controlPipeName = $"ABIRuntime.Control.{Convert.ToHexString(
                RandomNumberGenerator.GetBytes(24))}";
            _controlSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _controlPipe = CreateSecuredPipe(controlPipeName);

            progress?.Report(new PrivilegedProgress<TProgress>(
                PrivilegedStage.RequestingElevation, 15, "正在请求管理员权限"));
            RunFlage = ElevationLauncher.Start(
                controlPipeName, _controlSecret, _coreDllPath);
            if (RunFlage != 0)
                throw new InvalidOperationException($"高权限宿主启动失败：{RunFlage}。");

            progress?.Report(new PrivilegedProgress<TProgress>(
                PrivilegedStage.Connecting, 30, "正在连接高权限宿主"));

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await _controlPipe.WaitForConnectionAsync(timeout.Token).ConfigureAwait(false);

            PipeMessage handshake = await PipeProtocol.ReadAsync(
                _controlPipe, timeout.Token).ConfigureAwait(false);
            bool valid =
                handshake.Operation == OperationNames.Handshake &&
                handshake.Version == PipeProtocolVersion.Current &&
                CryptographicOperations.FixedTimeEquals(
                    handshake.Payload,
                    Convert.FromHexString(_controlSecret));
            if (!valid)
                throw new InvalidDataException("高权限宿主握手失败。");

            return _controlPipe;
        }
        catch
        {
            InvalidateControlPipe();
            throw;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private static NamedPipeServerStream CreateSecuredPipe(string pipeName)
    {
        SecurityIdentifier userSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("当前用户 SID 不可用。");
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(
            userSid, PipeAccessRights.FullControl, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }

    private void InvalidateControlPipe()
    {
        _controlPipe?.Dispose();
        _controlPipe = null;
        _controlSecret = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _connectionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            InvalidateControlPipe();
        }
        finally
        {
            _connectionGate.Release();
            _connectionGate.Dispose();
            _controlWriteGate.Dispose();
        }
    }
}

internal sealed class CancellationTokenSignal : IDisposable
{
    private readonly CancellationTokenRegistration _registration;
    private readonly TaskCompletionSource _source = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    internal CancellationTokenSignal(CancellationToken cancellationToken)
    {
        if (cancellationToken.CanBeCanceled)
        {
            _registration = cancellationToken.Register(
                static state => ((TaskCompletionSource)state!).TrySetResult(),
                _source);
        }
    }

    internal Task Task => _source.Task;

    public void Dispose() => _registration.Dispose();
}
