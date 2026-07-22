using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.ViewModel;

public sealed partial class ToolkitViewModel:ViewModelBase
{
    public ToolkitViewModel(IViewFactorys viewFactorys,ITaskManager taskManager)
    {
        ViewFactorys = viewFactorys;
        TaskManager = taskManager;
    }

    public IViewFactorys ViewFactorys { get; }

    public ITaskManager TaskManager { get; }

    [ObservableProperty]
    public partial ObservableCollection<TaskWrapper> Tasks { get; set; }

    [RelayCommand]
    void Loaded()
    {
        this.Tasks = TaskManager.GetTasks().Select(x=>x.Create()).ToObservableCollection();
    }

    [RelayCommand]
    void ShowAutoKuroToken()
    {
        var window = ViewFactorys.ShowAutoKruoTokenWindow();
        window.AppWindow.Show();
    }
}
