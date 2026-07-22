using Waves.Core.Contracts.Tasks;

namespace Waves.Core.Services.Tasks;

/// <summary>
/// 单次执行任务，不可计时执行
/// </summary>
public abstract class DefaultTaskServiceBase : ITaskService
{
    private CancellationTokenSource cts;

    public DefaultTaskServiceBase(SystemEventPublisher publisher)
    {
        Publisher = publisher;
    }

    public SystemEventPublisher Publisher { get; }

    public abstract string DisplayName { get; }

    public async Task CancelAsync()
    {
        if (cts != null)
            await this.cts.CancelAsync();
    }

    public virtual async Task InitializationAsync()
    {
        this.cts = new CancellationTokenSource();
    }

    public async Task InvokeAsync(CancellationToken token = default)
    {
        if(token == default)
        {
            await BeginAsync(cts.Token);
        }
        else
        {
            await BeginAsync(token);
        }
    }

    public abstract Task BeginAsync(CancellationToken token);
    public bool IsRuning() => false;
}
