using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using Waves.Core.Models.Tasks;

namespace Haiyu.ViewModel;

public sealed partial class ToolkitViewModel : ViewModelBase
{
    public ToolkitViewModel(IViewFactorys viewFactorys, ITaskManager taskManager)
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

    #region Moniter
    [ObservableProperty]
    public partial string? MoniterInvokeStr { get; set; }
    #endregion

    [RelayCommand]
    Task Loaded() => RunWhileAliveAsync(_ => RefreshTasks());

    [RelayCommand]
    async Task RefreshTasks()
    {
        this.Tasks = (await TaskManager.GetTasksAsync()).ToObservableCollection();
        this.MoniterInvokeStr =
            this.AppContext.WindowManager.IsWindowShow("MonitorTool") == false
                ? LanguageService.GetString("Display_Open")
                : LanguageService.GetString("Display_Close");
    }

    [RelayCommand]
    async Task ImportABIRuntime()
    {
        try
        {
            var zipFile =
                await this.AppContext.WindowManager.Shell.PickersService.GetFileOpenPicker([
                    "*.zip",
                ]);
            if (zipFile == null || string.IsNullOrWhiteSpace(zipFile.Path))
            {
                return;
            }
            using (ZipArchive zipArch = new ZipArchive(File.OpenRead(zipFile.Path)))
            {
                if (!zipArch.Entries.Any(x => x.Name == "Haiyu.ABI.dll"))
                {
                    await this.AppContext.WindowManager.Shell.TipShow.ShowMessageAsync(
                        "Import Error!",
                        Symbol.Clear
                    );
                    return;
                }
            }
            var moniter = this.AppContext.WindowManager.GetWindowContext("MonitorTool");
            if (moniter != null)
            {
                moniter.Close();
            }
            if(this.AppContext.ABIRuntimeService.Runtime != null)
            {
                await this.AppContext.WindowManager.Shell.TipShow.ShowMessageAsync(
                    LanguageService.GetString("Toolkit_WaitABIExit"),
                    Symbol.Message
                );
                await this.AppContext.ABIRuntimeService.Close();
                await Task.Delay(3000);
            }
            if (Directory.Exists(AppSettings.ABIRuntimeSavePath))
                Directory.Delete(AppSettings.ABIRuntimeSavePath, true);
            await this.AppContext.WindowManager.Shell.TipShow.ShowMessageAsync(
                LanguageService.GetString("Tool_StartImport"),
                Symbol.Message
            );
            IProgress<double> p = new Progress<double>(p => Debug.WriteLine(p));
            await ZipArchiveHelper.UnZipFileAsync(
                zipFile.Path,
                AppSettings.ABIRuntimeSavePath,
                p,
                this.CTS.Token
            );
            await this.AppContext.WindowManager.Shell.TipShow.ShowMessageAsync(
                LanguageService.GetString("Tool_ImportComplete"),
                Symbol.Message
            );
            await this.AppContext.ABIRuntimeService.Initialize(Waves.Settings.AppSettings.ABIRuntimeSavePath);
            await this.Loaded();
        }
        catch (Exception ex)
        {
            await this.AppContext.WindowManager.Shell.TipShow.ShowMessageAsync(
                LanguageService.GetString("Tool_ImportError")+ex.Message,
                Symbol.Message
            );
            return;
        }
    }

    [RelayCommand]
    void ShowAutoKuroToken()
    {
        ViewFactorys.ShowAutoKruoTokenWindow();
    }

    [RelayCommand]
    async Task ShowMonitorTool()
    {
        try
        {
            if (this.AppContext.WindowManager.IsWindowShow("MonitorTool"))
            {
                var window = this.AppContext.WindowManager.GetWindowContext("MonitorTool");
                if (window == null)
                    return;
                window.Close();
                this.MoniterInvokeStr = LanguageService.GetString("Display_Open");
            }
            else
            {
                ViewFactorys.ShowMonitorToolWindow();
                this.MoniterInvokeStr = LanguageService.GetString("Display_Close");
            }
        }
        catch (Exception)
        {

        }
        finally
        {
        }
    }

    [RelayCommand]
    async Task ShowClearMemoryTool()
    {
        await this.AppContext.WindowManager.Shell.DialogManager.ShowClearMemoryAsync();
    }

    [RelayCommand]
    void ShowMoniterSetting()
    {
        ViewFactorys.ShowMoniterSettingWindow();
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
                    await AppSettings.WriteAsync(
                        message.wrapper.AutoLaunche.ToString(),
                        message.wrapper.SettingName
                    );
                    break;
                default:
                    break;
            }
        });
    }
}
