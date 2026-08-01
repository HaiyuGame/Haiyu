using Waves.Settings;

namespace Haiyu.Services;

/// <summary>
/// 简单熔断器
/// </summary>
public class IoCircuitBreaker : IIoCircuitBreaker
{
    private SemaphoreSlim _semaphore;
    private int _currentMax;

    public IoCircuitBreaker(AppSettings appSettings)
    {
        AppSettings = appSettings;
        _currentMax = AppSettings.GetMaxIoConcurrentAsync().GetAwaiter().GetResult();
        _semaphore = new SemaphoreSlim(_currentMax, _currentMax);
    }

    public AppSettings AppSettings { get; }

    public void Release()
    {
        _semaphore.Release();
    }
    public bool TryAcquire()
    {
        var maxIoConcurrent = AppSettings.GetMaxIoConcurrentAsync().GetAwaiter().GetResult();
        if (maxIoConcurrent != _currentMax)
        {
            _currentMax = maxIoConcurrent;
            _semaphore = new SemaphoreSlim(_currentMax, _currentMax);
        }
        return _semaphore.Wait(0);
    }
}
