using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using Waves.Core.Common;
using Waves.Core.GameContext.ContextsV2.Waves;
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

    /// <summary>
    /// 鸣潮日志解密工具：定位 Saved\Logs 下所有 Client*.log，
    /// 解密 ELog 加密内容（明文原样复制）输出到用户选择的目录。
    /// </summary>
    [RelayCommand]
    async Task DecryptGameLogs()
    {
        // 游戏目录未设置时拒绝打开工具
        var gameFolder = await this.GameContextLastFolderAsync();
        if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder))
        {
            await this.AppContext.WindowManager.Shell.TipShow.ShowMessageAsync(
                LanguageService.GetStringByText("游戏目录未设置，请先在游戏页选择游戏目录"),
                Symbol.Clear
            );
            return;
        }

        var logsFolder = Path.Combine(gameFolder, "Client", "Saved", "Logs");
        if (!Directory.Exists(logsFolder))
        {
            await this.AppContext.WindowManager.Shell.TipShow.ShowMessageAsync(
                LanguageService.GetStringByText("日志文件夹不存在"),
                Symbol.Clear
            );
            return;
        }

        // 只处理 Client 前缀日志（Client.log 与 Client-backup-*.log）；
        // cgsdk_.log 未加密且每次启动被游戏覆盖、Logs\NGX 为 NVIDIA 配置，均不处理
        var logFiles = new DirectoryInfo(logsFolder)
            .EnumerateFiles("Client*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(x => x.LastWriteTime)
            .ToList();
        if (logFiles.Count == 0)
        {
            await this.AppContext.WindowManager.Shell.TipShow.ShowMessageAsync(
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

        var decryptedCount = 0;
        var copiedCount = 0;
        var failedCount = 0;
        foreach (var logFile in logFiles)
        {
            try
            {
                var input = await File.ReadAllBytesAsync(logFile.FullName, this.CTS.Token);
                var outputName = logFile.Name + ".decrypted.log";
                var outputPath = Path.Combine(outputFolder.Path, outputName);

                // UTF-8 BOM 开头即明文（-closeELog 生效时生成），原样复制；否则按 ELog 双 key 异或解密
                var encrypted = KrKeyHelper.IsELogEncrypted(input);
                var output = encrypted ? KrKeyHelper.TransformELog(input) : input;
                await File.WriteAllBytesAsync(outputPath, output, this.CTS.Token);

                if (encrypted)
                    decryptedCount++;
                else
                    copiedCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                Logger.WriteError($"解密日志失败 {logFile.Name}: {ex.Message}");
            }
        }

        if (failedCount > 0)
        {
            await this.AppContext.WindowManager.Shell.TipShow.ShowMessageAsync(
                LanguageService.FormatByText(
                    LanguageService.GetStringByText("解密完成，{0} 个文件失败，详见日志"),
                    failedCount
                ),
                Symbol.Clear
            );
        }
        else
        {
            await this.AppContext.WindowManager.Shell.TipShow.ShowMessageAsync(
                LanguageService.FormatByText(
                    LanguageService.GetStringByText("解密完成：解密{0}个，明文复制{1}个"),
                    decryptedCount,
                    copiedCount
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

    /// <summary>
    /// 读取最近一次在游戏页选择的游戏目录（与游戏页共享 GameLauncherBassFolder 配置）
    /// </summary>
    private async Task<string?> GameContextLastFolderAsync()
    {
        var contextName = await AppSettings.GetWavesAutoOpenContextAsync(this.CTS.Token);
        if (string.IsNullOrWhiteSpace(contextName))
        {
            contextName = nameof(WavesMainGameContextV2);
        }
        var context = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(contextName);
        return await context.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder,
            this.CTS.Token
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
