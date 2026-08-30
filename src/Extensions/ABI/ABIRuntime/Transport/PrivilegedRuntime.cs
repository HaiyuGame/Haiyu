using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using ABI.Models;
using ABIRuntime.Abstractions;

namespace ABIRuntime.Runtime;

public sealed class PrivilegedRuntime : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _coreDllPath;
    private NamedPipeServerStream? _pipe;
    private string? _secret;

    public int RunFlage { get; private set; }

    public PrivilegedRuntime(string coreDllPath)
    {
        if (string.IsNullOrWhiteSpace(coreDllPath) || !File.Exists(coreDllPath))
            throw new ArgumentException("Core DLL 路径不能为空或不存在。", nameof(coreDllPath));
        _coreDllPath = coreDllPath;
    }

    public async ValueTask<IPrivilegedResult<TResponse>> InvokeAsync<
        TRequest,
        TResponse,
        TProgress
    >(
        PrivilegedServiceContract<TRequest, TResponse, TProgress> contract,
        TRequest request,
        IProgress<IPrivilegedProgress<TProgress>>? progress = null,
        CancellationToken cancellationToken = default
    )
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(contract);
        progress?.Report(
            new PrivilegedProgress<TProgress>(PrivilegedStage.Preparing, 0, "正在准备请求")
        );

        try
        {
            TResponse response = await InvokeCoreAsync(
                    contract,
                    request,
                    progress,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return new PrivilegedResult<TResponse>(true, 0, "操作成功", response);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new PrivilegedResult<TResponse>(
                false,
                exception.HResult,
                exception.Message,
                null
            );
        }
    }

    private async ValueTask<TResponse> InvokeCoreAsync<TRequest, TResponse, TProgress>(
        PrivilegedServiceContract<TRequest, TResponse, TProgress> contract,
        TRequest request,
        IProgress<IPrivilegedProgress<TProgress>>? progress,
        CancellationToken cancellationToken
    )
        where TResponse : class
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            NamedPipeServerStream pipe = await EnsureConnectedAsync(progress, cancellationToken)
                .ConfigureAwait(false);
            Guid requestId = Guid.NewGuid();
            string json = JsonSerializer.Serialize(request, contract.RequestType);

            progress?.Report(
                new PrivilegedProgress<TProgress>(
                    PrivilegedStage.Executing,
                    40,
                    "正在发送高权限请求"
                )
            );
            await PipeProtocol
                .WriteAsync(
                    pipe,
                    new PipeMessage(
                        PipeMessageKind.Request,
                        requestId,
                        PipeProtocolVersion.Current,
                        contract.Operation,
                        json
                    ),
                    cancellationToken
                )
                .ConfigureAwait(false);

            bool cancellationSent = false;
            using var cancellationSignal = new CancellationTokenSignal(cancellationToken);
            using var cancellationGrace = new CancellationTokenSource();

            while (true)
            {
                Task<PipeMessage> readTask = PipeProtocol
                    .ReadAsync(pipe, cancellationGrace.Token)
                    .AsTask();

                if (!cancellationSent)
                {
                    Task completed = await Task.WhenAny(readTask, cancellationSignal.Task)
                        .ConfigureAwait(false);
                    if (completed == cancellationSignal.Task)
                    {
                        cancellationSent = true;
                        using var sendTimeout = new CancellationTokenSource(
                            TimeSpan.FromSeconds(2));
                        try
                        {
                            await PipeProtocol.WriteAsync(pipe,
                                new PipeMessage(PipeMessageKind.Cancel, requestId,
                                    PipeProtocolVersion.Current, contract.Operation, ""),
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

                if (
                    message.RequestId != requestId
                    || message.Version != PipeProtocolVersion.Current
                    || message.Operation != contract.Operation
                )
                {
                    throw new InvalidDataException("响应与当前 Service 请求不匹配。");
                }

                if (message.Kind == PipeMessageKind.Progress)
                {
                    TProgress? data = string.IsNullOrWhiteSpace(message.Payload)
                        ? default
                        : JsonSerializer.Deserialize(message.Payload, contract.ProgressType);
                    progress?.Report(
                        new PrivilegedProgress<TProgress>(
                            PrivilegedStage.Executing,
                            message.Percentage,
                            message.Message,
                            data
                        )
                    );
                    continue;
                }

                if (message.Kind == PipeMessageKind.Result)
                {
                    TResponse response =
                        JsonSerializer.Deserialize(message.Payload, contract.ResponseType)
                        ?? throw new InvalidDataException("Service 响应为空。");
                    progress?.Report(
                        new PrivilegedProgress<TProgress>(
                            PrivilegedStage.Completed,
                            100,
                            message.Message
                        )
                    );
                    return response;
                }

                if (message.Kind == PipeMessageKind.Cancelled)
                    throw new OperationCanceledException(message.Message, cancellationToken);

                throw new InvalidOperationException(
                    $"高权限 Service 失败：0x{message.StatusCode:X8} {message.Message}"
                );
            }
        }
        catch
        {
            // 失败后丢弃当前会话，避免残留消息影响下一次调用；主程序继续运行。
            _pipe?.Dispose();
            _pipe = null;
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask<NamedPipeServerStream> EnsureConnectedAsync<TProgress>(
        IProgress<IPrivilegedProgress<TProgress>>? progress,
        CancellationToken cancellationToken
    )
    {
        if (_pipe is { IsConnected: true })
            return _pipe;
        _pipe?.Dispose();

        string pipeName = $"ABIRuntime.{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}";
        _secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        SecurityIdentifier userSid =
            WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("当前用户 SID 不可用。");
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(
            new PipeAccessRule(userSid, PipeAccessRights.FullControl, AccessControlType.Allow)
        );
        _pipe = NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security
        );

        progress?.Report(
            new PrivilegedProgress<TProgress>(
                PrivilegedStage.RequestingElevation,
                15,
                "正在请求管理员权限"
            )
        );
        var hint =  ElevationLauncher.Start(pipeName, _secret, _coreDllPath);
        this.RunFlage = hint;
        progress?.Report(
            new PrivilegedProgress<TProgress>(PrivilegedStage.Connecting, 30, "正在连接高权限宿主")
        );

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await _pipe.WaitForConnectionAsync(timeout.Token).ConfigureAwait(false);
        PipeMessage handshake = await PipeProtocol
            .ReadAsync(_pipe, timeout.Token)
            .ConfigureAwait(false);
        bool valid =
            handshake.Operation == OperationNames.Handshake
            && handshake.Version == PipeProtocolVersion.Current
            && CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(handshake.Payload),
                Convert.FromHexString(_secret)
            );
        if (!valid)
        {
            _pipe.Dispose();
            _pipe = null;
            throw new InvalidDataException("高权限宿主握手失败。");
        }

        return _pipe;
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _pipe?.Dispose();
            _pipe = null;
            _secret = null;
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}

/// <summary>把调用方 Token 转换为可等待的一次性取消信号。</summary>
internal sealed class CancellationTokenSignal : IDisposable
{
    private readonly CancellationTokenRegistration _registration;
    private readonly TaskCompletionSource _source = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    internal CancellationTokenSignal(CancellationToken cancellationToken)
    {
        if (cancellationToken.CanBeCanceled)
            _registration = cancellationToken.Register(static state =>
                ((TaskCompletionSource)state!).TrySetResult(), _source);
    }

    internal Task Task => _source.Task;

    public void Dispose() => _registration.Dispose();
}
