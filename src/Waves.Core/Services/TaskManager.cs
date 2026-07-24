using Waves.Core.Contracts.Tasks;
using Waves.Core.Models.Tasks;

namespace Waves.Core.Services;

public class TaskManager : ITaskManager
{
    private readonly IDictionary<string, ITaskService> _tasks;

    public AppSettings AppSettings { get; }

    public TaskManager(AppSettings appSettings)
    {
        _tasks = new Dictionary<string, ITaskService>();
        AppSettings = appSettings;
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

    public async Task<IEnumerable<TaskWrapper>> GetTasksAsync()
    {
        var tasks = _tasks
        .Values
        .OfType<ITaskName>()
        .Select(async x => new TaskWrapper(
            x.Guid,
            x.DisplayName,
            x.Description,
            x is ITaskService service && service.IsRuning,
            x.Note,
            Adaptives.BoolAdaptive.Instance.GetForward(
                await AppSettings.ReadAsync(x.Note)
            )
        ));

        return await Task.WhenAll(tasks);
    }

    public async Task InvokeTaskAsync(string taskName, CancellationToken cts = default)
    {
        var tasks = this._tasks.Values.OfType<ITaskName>().FirstOrDefault(t => t.Guid == taskName);
        if (tasks == null)
            return;
        if (tasks is ITaskService taskService)
        {
            await taskService.InvokeAsync(cts);
        }
    }

    public async Task StartTaskAsync(string taskName)
    {
        var tasks = this._tasks.Values.OfType<ITaskName>().FirstOrDefault(t => t.Guid == taskName);
        if (tasks == null)
            return;
        if (tasks is ITaskService taskService)
        {
            await taskService.InitializationAsync();
        }
    }

    public async Task StopTaskAsync(string taskName)
    {
        var tasks = this._tasks.Values.OfType<ITaskName>().FirstOrDefault(t => t.Guid == taskName);
        if (tasks == null)
            return;
        if (tasks is ITaskService taskService)
        {
            await taskService.CancelAsync();
        }
    }

    public async Task InitializeAutoLaunchTasksAsync()
    {
        foreach (var item in this._tasks)
        {
            var task = item.Value;
            if(task is ITaskName name)
            {
                var value = await AppSettings.ReadAsync(name.Note);
                var isRun = BoolAdaptive.Instance.GetForward(value);
                if (isRun)
                {
                    await task.InitializationAsync();
                }
            }
        }
    }
}
