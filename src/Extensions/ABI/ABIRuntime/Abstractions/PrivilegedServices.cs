using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using ABI.Models;
using ABIRuntime.Runtime;
using MemoryPack;

namespace ABIRuntime.Abstractions;

public interface IPrivilegedResult<out TData>
{
    bool IsSuccess { get; }
    int StatusCode { get; }
    string Message { get; }
    TData? Data { get; }
}

public interface IPrivilegedProgress<out TData>
{
    PrivilegedStage Stage { get; }
    int Percentage { get; }
    string Message { get; }
    TData? Data { get; }
}

public sealed record PrivilegedResult<TData>(
    bool IsSuccess,
    int StatusCode,
    string Message,
    TData? Data) : IPrivilegedResult<TData>;

public sealed record PrivilegedProgress<TData>(
    PrivilegedStage Stage,
    int Percentage,
    string Message,
    TData? Data = default) : IPrivilegedProgress<TData>;

public enum PrivilegedStage
{
    Preparing,
    RequestingElevation,
    Connecting,
    Executing,
    Completed,
}

/// <summary>客户端与高权限宿主共享的强类型服务契约。</summary>
public sealed record PrivilegedServiceContract<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TRequest,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProgress>(
    string Operation,
    JsonTypeInfo<TRequest> RequestType,
    JsonTypeInfo<TResponse> ResponseType,
    JsonTypeInfo<TProgress> ProgressType)
    where TResponse : class;

/// <summary>高权限业务服务；实现类无需了解 UAC、管道或 Rundll32。</summary>
public interface IPrivilegedService<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TRequest,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProgress>
    where TResponse : class
{
    PrivilegedServiceContract<TRequest, TResponse, TProgress> Contract { get; }

    ValueTask<TResponse> ExecuteAsync(
        TRequest request,
        IProgress<TProgress> progress,
        CancellationToken cancellationToken);
}

public interface IPrivilegedServiceRegistry
{
    void Add<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TRequest,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProgress>(
        IPrivilegedService<TRequest, TResponse, TProgress> service)
        where TResponse : class;
}

/// <summary>宿主端服务注册表，将操作名映射到强类型 Service。</summary>
public sealed class PrivilegedServiceRegistry : IPrivilegedServiceRegistry
{
    private readonly Dictionary<string, IServiceInvoker> _services =
        new(StringComparer.Ordinal);

    public void Add<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TRequest,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProgress>(
        IPrivilegedService<TRequest, TResponse, TProgress> service)
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(service);

        if (!_services.TryAdd(service.Contract.Operation,
            new ServiceInvoker<TRequest, TResponse, TProgress>(service)))
        {
            throw new InvalidOperationException(
                $"高权限 Service 已注册：{service.Contract.Operation}");
        }
    }

    internal bool TryGet(string operation, out IServiceInvoker invoker) =>
        _services.TryGetValue(operation, out invoker!);
}

internal interface IServiceInvoker
{
    ValueTask InvokeAsync(Stream pipe, PipeMessage request,
        CancellationToken cancellationToken);
}

/// <summary>上下层转递：MemoryPack 请求 → 强类型 Service → 二进制进度/响应。</summary>
internal sealed class ServiceInvoker<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TRequest,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProgress>(
    IPrivilegedService<TRequest, TResponse, TProgress> service) : IServiceInvoker
    where TResponse : class
{
    public async ValueTask InvokeAsync(Stream pipe, PipeMessage request,
        CancellationToken cancellationToken)
    {
        ABIMemoryPack.EnsureFormatters();
        TRequest input = MemoryPackSerializer.Deserialize<TRequest>(
            request.Payload, ABIMemoryPack.Options)!;

        if (input is null)
            throw new InvalidDataException("Service 请求数据为空。");

        var progress = new HostProgress<TProgress>(pipe, request);

        TResponse output = await service.ExecuteAsync(
            input, progress, cancellationToken).ConfigureAwait(false);

        byte[] payload = MemoryPackSerializer.Serialize(output, ABIMemoryPack.Options);
        await PipeProtocol.WriteAsync(pipe,
            new PipeMessage(PipeMessageKind.Result, request.RequestId, request.Version,
                request.Operation, payload, 100, "操作完成"), cancellationToken)
            .ConfigureAwait(false);
    }
}

internal sealed class HostProgress<TProgress>(
    Stream pipe,
    PipeMessage request) : IProgress<TProgress>
{
    private readonly object _sync = new();

    public void Report(TProgress value)
    {
        lock (_sync)
        {
            byte[] payload = MemoryPackSerializer.Serialize(value, ABIMemoryPack.Options);
            PipeProtocol.WriteAsync(pipe,
                new PipeMessage(PipeMessageKind.Progress, request.RequestId,
                    request.Version, request.Operation, payload, 0, "业务进度"),
                CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }
    }
}
