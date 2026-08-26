namespace Waves.Core.Services;

/// <summary>
/// 基于原子计数的 IO 熔断器。
/// </summary>
public sealed class IoCircuitBreaker : IIoCircuitBreaker
{
    private int _runningCount;

    public IoCircuitBreaker(AppSettings appSettings)
    {
        AppSettings = appSettings;
    }

    public AppSettings AppSettings { get; }

    public bool TryAcquire()
    {
        var maxIoConcurrent = Math.Max(
            1,
            AppSettings.GetMaxIoConcurrentAsync().ConfigureAwait(false).GetAwaiter().GetResult()
        );

        while (true)
        {
            var current = Volatile.Read(ref _runningCount);
            if (current >= maxIoConcurrent)
                return false;

            if (Interlocked.CompareExchange(ref _runningCount, current + 1, current) == current)
                return true;
        }
    }

    public void Release()
    {
        while (true)
        {
            var current = Volatile.Read(ref _runningCount);
            if (current == 0)
                return;

            if (Interlocked.CompareExchange(ref _runningCount, current - 1, current) == current)
                return;
        }
    }
}
