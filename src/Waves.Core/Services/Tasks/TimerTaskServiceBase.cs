using Waves.Core.Contracts.Tasks;

namespace Waves.Core.Services.Tasks;

public abstract class TimerTaskServiceBase : ITimerTaskService, IAsyncDisposable
{
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private bool _disposed;

    protected TimerTaskServiceBase(
        SystemEventPublisher publisher,
        LoggerService logger
    )
    {
        Publisher = publisher;
        Logger = logger;
    }

    public long CheckDelay { get; set; }

    public SystemEventPublisher Publisher { get; }
    public LoggerService Logger { get; }

    public virtual bool CheckRun()
    {
        return true;
    }

    public virtual async Task InitializationAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var period = TimeSpan.FromSeconds(CheckDelay);
        if (period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CheckDelay),
                CheckDelay,
                "任务检查周期必须大于 0 秒。"
            );
        }

        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await StopCoreAsync().ConfigureAwait(false);

            _timer = new PeriodicTimer(period);
            _cts = new CancellationTokenSource();
            _loopTask = LoopRunAsync(_timer, _cts.Token);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task LoopRunAsync(PeriodicTimer timer, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                    break;

                if (CheckRun())
                {
                    await InvokeAsync(token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.WriteError(ex.ToString());
                Publisher.Publish(
                    new()
                    {
                        Delay = TimeSpan.FromSeconds(2).TotalSeconds,
                        Message = $"任务执行错误，详情请看日志",
                    }
                );
            }
        }
    }

    public async Task CancelAsync()
    {
        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task StopCoreAsync()
    {
        var cts = _cts;
        var timer = _timer;
        var loopTask = _loopTask;

        _cts = null;
        _timer = null;
        _loopTask = null;

        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        timer?.Dispose();

        if (loopTask is not null)
        {
            try
            {
                await loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cts?.IsCancellationRequested == true)
            {
                // 正常取消。
            }
        }

        cts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }

        GC.SuppressFinalize(this);
    }

    public abstract Task InvokeAsync(CancellationToken token = default);
}
