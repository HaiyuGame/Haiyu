using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Models.Tasks;

namespace Haiyu.ViewModel;

public sealed partial class ToolkitViewModel:ViewModelBase
{
    public ToolkitViewModel(IViewFactorys viewFactorys,ITaskManager taskManager)
    {
        ViewFactorys = viewFactorys;
        TaskManager = taskManager;
        ReisterMessager();
    }

    private void ReisterMessager()
    {
        WeakReferenceMessenger.Default.Register<SendTaskMessager>(this, SendTaskMethod);
    }


    public IViewFactorys ViewFactorys { get; }

    public ITaskManager TaskManager { get; }

    [ObservableProperty]
    public partial ObservableCollection<TaskWrapper> Tasks { get; set; }

    [RelayCommand]
    Task Loaded() => RunWhileAliveAsync(_ => RefreshTasks());

    [RelayCommand]
    async Task RefreshTasks()
    {
        this.Tasks =(await TaskManager.GetTasksAsync()).ToObservableCollection();
    }

    [RelayCommand]
    void ShowAutoKuroToken()
    {
        var window = ViewFactorys.ShowAutoKruoTokenWindow();
        window.AppWindow.Show();
    }


    private void SendTaskMethod(object recipient, SendTaskMessager message)
    {
        _ = RunWhileAliveAsync(async token =>
        {
        switch (message.type)
        {
            case SendTaskType.Start:
                await TaskManager.StartTaskAsync(message.wrapper.Guid);
                await this.RefreshTasks();
                break;
            case SendTaskType.Stop:
                await TaskManager.StopTaskAsync(message.wrapper.Guid);
                await this.RefreshTasks();
                break;
            case SendTaskType.Invoke:
                await TaskManager.InvokeTaskAsync(message.wrapper.Guid, token);
                break;
            case SendTaskType.Launche:
                await AppSettings.WriteAsync(message.wrapper.AutoLaunche.ToString(), message.wrapper.SettingName);
                break;
            default:
                break;
        }
        });
    }
}
