using Waves.Core.Contracts.Tasks;
using Waves.Core.Models.Tasks;

namespace Waves.Core.Contracts;

public interface ITaskManager
{
    public void RegsiterTask<ITask>(ITask task)
        where ITask : ITaskService, ITaskName;


    public Task<IEnumerable<TaskWrapper>> GetTasksAsync();

    /// <summary>
    /// 单次执行
    /// </summary>
    /// <param name="taskName"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    public Task InvokeTaskAsync(string taskName, CancellationToken cts = default);

    /// <summary>
    /// 开始任务
    /// </summary>
    /// <param name="taskName"></param>
    /// <returns></returns>
    public Task StartTaskAsync(string taskName);

    /// <summary>
    /// 停止任务
    /// </summary>
    /// <param name="taskName"></param>
    /// <returns></returns>
    public Task StopTaskAsync(string taskName);

    /// <summary>
    /// 读取任务进行自启
    /// </summary>
    /// <returns></returns>
    public Task InitializeAutoLaunchTasksAsync();
}
