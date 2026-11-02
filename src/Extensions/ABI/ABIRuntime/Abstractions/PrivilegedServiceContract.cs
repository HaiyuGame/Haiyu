using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace ABIRuntime.Abstractions;

/// <summary>客户端与高权限宿主共享的强类型服务契约。</summary>
public sealed record PrivilegedServiceContract<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TRequest,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProgress,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse
>(
    string Operation,
    JsonTypeInfo<TRequest> RequestType,
    JsonTypeInfo<TProgress> ProgressType,
    JsonTypeInfo<TResponse> ResponseType
)
    where TResponse : class;
