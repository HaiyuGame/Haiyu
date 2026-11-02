namespace ABIRuntime.Abstractions;

public sealed record PrivilegedProgress<TData>(
    PrivilegedStage Stage,
    int Percentage,
    string Message,
    TData? Data = default
) : IPrivilegedProgress<TData>;
