using Waves.Core.Contracts.Tasks;

namespace Waves.Core.Services;

public class TaskManager : ITaskManager
{
    private readonly IDictionary<string, ITaskService> _tasks;

    public TaskManager()
    {
        _tasks = new Dictionary<string, ITaskService>();
    }

    public void RegsiterTask<ITask>(ITask task)
        where ITask : ITaskService, ITaskName
    {
        var name = typeof(ITask).FullName;
        ArgumentNullException.ThrowIfNull(name);
        if (_tasks.ContainsKey(name))
        {
            return;
        }
        this._tasks.Add(name, task);
    }

    public IEnumerable<Tuple<string, string,string>> GetTasks() =>
        _tasks
            .Values.OfType<ITaskName>()
            .Select(x => Tuple.Create<string, string,string>(x.Guid,x.DisplayName, x.Description));

    public async Task InvokeTaskAsync(string taskName, CancellationToken cts = default)
    {
        if (_tasks.TryGetValue(taskName, out var targetTask))
        {
            await targetTask.InvokeAsync(cts);
        }
    }

    public async Task StartTaskAsync(string taskName)
    {
        if (_tasks.TryGetValue(taskName, out var targetTask))
        {
            await targetTask.InitializationAsync();
        }
    }

    public async Task StopTaskAsync(string taskName)
    {
        if (_tasks.TryGetValue(taskName, out var targetTask))
        {
            await targetTask.CancelAsync();
        }
    }
}
