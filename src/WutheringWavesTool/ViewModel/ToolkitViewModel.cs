using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using Waves.Core.Models.Enums;
using Waves.Core.Models.Tasks;

namespace Haiyu.ViewModel;

public sealed partial class ToolkitViewModel : ViewModelBase
{
    public ToolkitViewModel(
        IViewFactorys viewFactorys,
        ITaskManager taskManager,
        IGameLogService gameLogService
    )
    {
        ViewFactorys = viewFactorys;
        TaskManager = taskManager;
        GameLogService = gameLogService;
        ReisterMessager();
    }

    private void ReisterMessager()
    {
        WeakReferenceMessenger.Default.Register<SendTaskMessager>(this, SendTaskMethod);
    }

    public IViewFactorys ViewFactorys { get; }

    public ITaskManager TaskManager { get; }

    public IGameLogService GameLogService { get; }

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

    /// <summary>
    /// 鸣潮日志解密工具：定位 Saved\Logs 下所有 Client*.log，
    /// 解密 ELog 加密内容（明文原样复制）输出到用户选择的目录。
    /// </summary>
    [RelayCommand]
    async Task DecryptGameLogs()
    {
        // 核心层定位日志文件，按状态给出提示
        var locate = await GameLogService.LocateGameLogsAsync(this.CTS.Token);
        var tipShow = this.AppContext.WindowManager.Shell.TipShow;
        switch (locate.State)
        {
            case GameLogLocateState.GameFolderMissing:
                await tipShow.ShowMessageAsync(
                    LanguageService.GetStringByText("游戏目录未设置，请先在游戏页选择游戏目录"),
                    Symbol.Clear
                );
                return;
            case GameLogLocateState.LogsFolderMissing:
                await tipShow.ShowMessageAsync(
                    LanguageService.GetStringByText("日志文件夹不存在"),
                    Symbol.Clear
                );
                return;
            case GameLogLocateState.NoLogFiles:
                await tipShow.ShowMessageAsync(
                    LanguageService.GetStringByText("日志文件不存在"),
                    Symbol.Clear
                );
                return;
        }

        // 让用户选择解密结果输出目录
        var outputFolder = await this.AppContext.WindowManager.Shell.PickersService.GetFolderPicker();
        if (outputFolder == null || string.IsNullOrWhiteSpace(outputFolder.Path))
        {
            return;
        }

        var result = await GameLogService.DecryptGameLogsAsync(
            locate.LogFiles,
            outputFolder.Path,
            this.CTS.Token
        );

        if (result.HasKeyMismatch)
        {
            // key 不匹配：解密结果不是有效明文，已拦截未导出
            await tipShow.ShowMessageAsync(
                LanguageService.FormatByText(
                    LanguageService.GetStringByText("解密校验未通过：游戏可能已更换加密方式，{0} 个文件未导出，请更新启动器后重试"),
                    result.KeyMismatchCount
                ),
                Symbol.Clear
            );
        }
        else if (result.HasFailed)
        {
            await tipShow.ShowMessageAsync(
                LanguageService.FormatByText(
                    LanguageService.GetStringByText("解密完成，{0} 个文件失败，详见日志"),
                    result.FailedCount
                ),
                Symbol.Clear
            );
        }
        else
        {
            await tipShow.ShowMessageAsync(
                LanguageService.FormatByText(
                    LanguageService.GetStringByText("解密完成：解密{0}个，明文复制{1}个"),
                    result.DecryptedCount,
                    result.CopiedCount
                ),
                Symbol.Accept
            );
        }

        // 完成后打开输出目录（Windows 资源管理器）
        WindowExtension.ShellExecute(
            IntPtr.Zero,
            "open",
            outputFolder.Path,
            null,
            null,
            WindowExtension.SW_SHOWNORMAL
        );
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
