namespace ABIRuntime.Abstractions;

public sealed record PrivilegedResult<TData>(
    bool IsSuccess,
    int StatusCode,
    string Message,
    TData? Data
) : IPrivilegedResult<TData>;
