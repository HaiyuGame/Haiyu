using System.Diagnostics.CodeAnalysis;

namespace ABIRuntime.Abstractions;

/// <summary>高权限业务服务；实现类无需了解 UAC、管道或 Rundll32。</summary>
public interface IPrivilegedService<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TRequest,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProgress,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse
>
    where TResponse : class
{
    PrivilegedServiceContract<TRequest, TProgress, TResponse> Contract { get; }

    ValueTask<TResponse> ExecuteAsync(
        TRequest request,
        IProgress<TProgress> progress,
        CancellationToken cancellationToken
    );
}
