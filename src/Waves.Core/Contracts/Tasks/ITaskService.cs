namespace Waves.Core.Contracts.Tasks;

public interface ITaskService
{

    /// <summary>
    /// 系统消息出口
    /// </summary>
    public SystemEventPublisher Publisher { get; }
    
    /// <summary>
    /// 执行任务
    /// </summary>
    /// <returns></returns>
    public Task InvokeAsync(CancellationToken token = default);

    /// <summary>
    /// 初始化
    /// </summary>
    /// <returns></returns>
    public Task InitializationAsync();

    /// <summary>
    /// 取消
    /// </summary>
    /// <returns></returns>
    public Task CancelAsync();
}

public interface ITaskName
{

    public string DisplayName { get; }

    public string Description { get; }

    public string Guid { get; }
}
