using System.Diagnostics;

namespace Waves.Core.Common;

public class SpeedLimiter
{
    private long _maxBytesPerSecond;
    private long _bytesRemaining;
    private DateTime _lastUpdate;
    private readonly object _lock = new object();
    private long _bytesPerSecond;
    private int _tokenBucket;

    private readonly SemaphoreSlim _sync = new(1, 1);

    public async Task SetBytesPerSecondAsync(long value)
    {
        await _sync.WaitAsync();
        try
        {
            _bytesPerSecond = value;
            _maxBytesPerSecond = value;
            _bytesRemaining = value;
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task LimitAsync(int bytesTransferred,CancellationToken token = default)
    {
        if (_bytesPerSecond == 0)
            return;
        await _sync.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var now = DateTime.UtcNow;
            var elapsedSeconds = (now - _lastUpdate).TotalSeconds;

            var newTokens = (long)(_maxBytesPerSecond * elapsedSeconds);
            _tokenBucket = (int)Math.Min(_tokenBucket + newTokens, _maxBytesPerSecond);

            _tokenBucket -= bytesTransferred;
            _lastUpdate = now;

            if (_tokenBucket >= 0)
                return;

            var deficit = -_tokenBucket;
            var waitSeconds = (double)deficit / _maxBytesPerSecond;

            _tokenBucket += (int)(_maxBytesPerSecond * waitSeconds);
            _lastUpdate = now.AddSeconds(waitSeconds);

            await Task.Delay(TimeSpan.FromSeconds(waitSeconds),token).ConfigureAwait(false);
        }
        finally
        {
            _sync.Release();
        }
    }
}

/// <summary>
/// 速度转换
/// </summary>
public static class SpeedExtensions
{
    public static long UnitKB(this long value) => value * 1024;

    public static long UnitMB(this long value) => value * 1024 * 1024;

    public static long UnitGB(this long value) => value * 1024 * 1024 * 1024;
}
