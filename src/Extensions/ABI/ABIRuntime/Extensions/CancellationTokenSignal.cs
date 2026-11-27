namespace ABIRuntime.Extensions;

internal sealed class CancellationTokenSignal : IDisposable
{
    private readonly CancellationTokenRegistration _registration;
    private readonly TaskCompletionSource _source = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    internal CancellationTokenSignal(CancellationToken cancellationToken)
    {
        if (cancellationToken.CanBeCanceled)
        {
            _registration = cancellationToken.Register(
                static state => ((TaskCompletionSource) state!).TrySetResult(),
                _source);
        }
    }

    internal Task Task => _source.Task;

    public void Dispose() => _registration.Dispose();
}
