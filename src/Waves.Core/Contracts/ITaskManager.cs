using Waves.Core.Contracts.Tasks;

namespace Waves.Core.Contracts;

public interface ITaskManager
{
    public Task RegsiterTaskAsync<ITask>(ITask task)
        where ITask : ITaskService, ITaskName;


    public IEnumerable<string> GetTasks();

    public Task InvokeTaskAsync(string taskName, CancellationToken cts = default);


    public Task StartTaskAsync(string taskName);

    public Task StopTaskAsync(string taskName);
}
