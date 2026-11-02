namespace ABIRuntime.Abstractions;

public interface IPrivilegedProgress<out TData>
{
    PrivilegedStage Stage { get; }
    int Percentage { get; }
    string Message { get; }
    TData? Data { get; }
}
