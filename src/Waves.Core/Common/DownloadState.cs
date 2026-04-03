using System.Collections.Concurrent;
using Waves.Core.Models.Downloader;

namespace Waves.Core.Common;

public sealed class DownloadState
{
    internal volatile bool _isPaused;
    private long _currentBytes;

    public SpeedLimiter SpeedLimiter { get; private set; }
    public bool IsActive { get; set; }
    public CancellationTokenSource CancelToken { get; set; }
    public PauseToken PauseToken => new PauseToken(this);

    public DownloadState()
    {
        SpeedLimiter = new SpeedLimiter();
        _isPaused = false;
    }


    public bool IsPaused => _isPaused;

    public bool IsStop { get; internal set; }

    public async Task SetSpeedLimitAsync(long bytesPerSecond)
    {
        var newLimiter = new SpeedLimiter();
        await newLimiter.SetBytesPerSecondAsync(bytesPerSecond);
        SpeedLimiter = newLimiter;
    }

    public Task<bool> PauseAsync()
    {
        Volatile.Write(ref _isPaused, true);
        IsActive = false;
        return Task.FromResult(true);
    }

    public Task<bool> ResumeAsync()
    {
        Volatile.Write(ref _isPaused, false);
        IsActive = true;
        return Task.FromResult(true);
    }
}

public readonly struct PauseToken
{
    private readonly DownloadState _state;

    public PauseToken(DownloadState state) => _state = state;

    /// <summary>
    /// 异步等待暂停状态结束（无锁轮询）
    /// </summary>
    public async ValueTask WaitIfPausedAsync()
    {
        while (Volatile.Read(ref _state._isPaused))
        {
            await Task.Delay(100, _state.CancelToken.Token); // 低延迟轮询
        }
    }
}
