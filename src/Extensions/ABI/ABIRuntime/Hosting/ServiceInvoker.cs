using System.Diagnostics.CodeAnalysis;
using ABI.Models;
using ABIRuntime.Abstractions;
using MemoryPack;

namespace ABIRuntime.Runtime;

/// <summary>上下层转递：MemoryPack 请求 → 强类型 Service → 二进制进度/响应。</summary>
internal sealed class ServiceInvoker<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TRequest,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProgress,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse
>(IPrivilegedService<TRequest, TProgress, TResponse> service) : IServiceInvoker
    where TResponse : class
{
    public async ValueTask InvokeAsync(
        Stream pipe,
        PipeMessage request,
        CancellationToken cancellationToken
    )
    {
        ABIMemoryPack.EnsureFormatters();
        TRequest input = MemoryPackSerializer.Deserialize<TRequest>(
            request.Payload,
            ABIMemoryPack.Options
        )!;

        if (input is null)
            throw new InvalidDataException("Service 请求数据为空。");

        var progress = new HostProgress<TProgress>(pipe, request);

        TResponse output = await service
            .ExecuteAsync(input, progress, cancellationToken)
            .ConfigureAwait(false);

        byte[] payload = MemoryPackSerializer.Serialize(output, ABIMemoryPack.Options);
        await PipeProtocol
            .WriteAsync(
                pipe,
                new PipeMessage(
                    PipeMessageKind.Result,
                    request.RequestId,
                    request.Version,
                    request.Operation,
                    payload,
                    100,
                    "操作完成"
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
