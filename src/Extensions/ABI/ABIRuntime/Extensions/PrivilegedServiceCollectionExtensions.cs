using System.Diagnostics.CodeAnalysis;
using ABIRuntime.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace ABIRuntime.Abstractions;

/// <summary>通过操作名注册强类型服务，由 DI 管理每次请求的服务实例。</summary>
public static class PrivilegedServiceCollectionExtensions
{
    public static IServiceCollection AddPrivilegedService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TRequest,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProgress,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService
    >(this IServiceCollection services,
        PrivilegedServiceContract<TRequest, TProgress, TResponse> contract)
        where TResponse : class
        where TService : class, IPrivilegedService<TRequest, TProgress, TResponse>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentException.ThrowIfNullOrWhiteSpace(contract.Operation);

        if (services.Any(descriptor => descriptor.IsKeyedService &&
            descriptor.ServiceType == typeof(IServiceInvoker) &&
            Equals(descriptor.ServiceKey, contract.Operation)))
        {
            throw new InvalidOperationException($"高权限 Service 已注册：{contract.Operation}");
        }

        services.AddKeyedScoped<IPrivilegedService<TRequest, TProgress, TResponse>, TService>(
            contract.Operation);
        services.AddKeyedScoped<IServiceInvoker>(contract.Operation, (provider, key) =>
            new ServiceInvoker<TRequest, TProgress, TResponse>(
                provider.GetRequiredKeyedService<IPrivilegedService<TRequest, TProgress, TResponse>>(key)));
        return services;
    }
}
