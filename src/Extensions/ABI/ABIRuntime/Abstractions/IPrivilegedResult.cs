namespace ABIRuntime.Abstractions;

public interface IPrivilegedResult<out TData>
{
    bool IsSuccess { get; }
    int StatusCode { get; }
    string Message { get; }
    TData? Data { get; }
}
