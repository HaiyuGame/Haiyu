using ABI.Models;
using ABIRuntime.Abstractions;
using Haiyu.Common.Contracts;

namespace Haiyu.ViewModel.DialogViewModels;

public partial class ClearMemoryViewModel : DialogViewModelBase
{
    Progress<IPrivilegedProgress<CleanMemoryProgress>>? _cleanProgress = null;
    Progress<IPrivilegedProgress<CMonitorProgress>>? _moniterProgress = null;

    private Task cleanTask;

    public ClearMemoryViewModel(DialogSession session)
        : base(session) { }

    [ObservableProperty]
    public partial double MemoryTotal { get; set; }

    [ObservableProperty]
    public partial double MemoryUsed { get; set; }

    [ObservableProperty]
    public partial string MemoryUsedStr { get; set; }

    [ObservableProperty]
    public partial int Progress { get; set; }

    [ObservableProperty]
    public partial string ProgressStr { get; set; }

    [RelayCommand]
    async Task Loaded()
    {
        bool initialized = await AppContext.ABIRuntimeService.Initialize(
            Waves.Settings.AppSettings.ABIRuntimeSavePath
        );
        _moniterProgress = new Progress<IPrivilegedProgress<CMonitorProgress>>(
            (s) =>
            {
                if (s.Stage == PrivilegedStage.Executing && s.Data is { } data)
                {
                    if (data.data == null || data.data.Memory == null)
                        return;
                    this.MemoryTotal = data.data.Memory.Total;
                    this.MemoryUsed = data.data.Memory.Used;
                    this.MemoryUsedStr =
                        $"{Math.Round(
                        data.data.Memory.Used / data.data.Memory.Total * 100,
                        2
                    )}%";
                }
            }
        );
        _ = Task.Run(() => MonitorAsync(_moniterProgress, this.CTS));
    }

    [RelayCommand]
    async Task InvokeClear()
    {
        bool initialized = await AppContext.ABIRuntimeService.Initialize(
            Waves.Settings.AppSettings.ABIRuntimeSavePath
        );
        if (!initialized || AppContext.ABIRuntimeService.Runtime is null || !IsAlive)
        {
            Debug.WriteLine("监控运行时初始化失败。");
            return;
        }
        _cleanProgress = new Progress<IPrivilegedProgress<CleanMemoryProgress>>(
            (s) =>
            {
                if (s.Stage == PrivilegedStage.Executing && s.Data is { } data)
                {
                    Progress = s.Percentage;
                    ProgressStr = s.Message;
                }
            }
        );
        cleanTask = CleanerAsync(_cleanProgress, this.CTS);
        await cleanTask;
    }

    public async Task CleanerAsync(
        Progress<IPrivilegedProgress<CleanMemoryProgress>> progress,
        CancellationTokenSource token
    )
    {
        try
        {
            if (AppContext.ABIRuntimeService.Runtime == null)
                return;
            IPrivilegedResult<RunResult> result =
                await AppContext.ABIRuntimeService.Runtime!.InvokeAsync(
                    ABIRuntime.Contract.CleanMemoryContract,
                    new CleanMemoryRequest(true, true, true, true, true, true, true, true, "Haiyu"),
                    progress,
                    token.Token
                );

            if (!result.IsSuccess)
            {
                this.AppContext.WindowManager.Shell.TryInvoke(() =>
                {
                    this.AppContext.WindowManager.Shell.TipShow.ShowMessageAsync(
                        $"清理失败：0x{result.StatusCode:X8} {result.Message}",
                        Symbol.Message
                    );
                });
            }
        }
        catch (OperationCanceledException) when (token.Token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            this.AppContext.WindowManager.Shell.TryInvoke(() =>
            {
                this.AppContext.WindowManager.Shell.TipShow.ShowMessageAsync(
                    $"{exception.Message}",
                    Symbol.Message
                );
            });
        }
    }

    public async Task MonitorAsync(
        Progress<IPrivilegedProgress<CMonitorProgress>> progress,
        CancellationTokenSource token
    )
    {
        try
        {
            if (AppContext.ABIRuntimeService.Runtime == null)
                return;
            IPrivilegedResult<RunResult> result =
                await AppContext.ABIRuntimeService.Runtime!.InvokeAsync(
                    ABIRuntime.Contract.ComputerMonitorContract,
                    new CMonitorRequest(),
                    progress,
                    token.Token
                );

            if (!result.IsSuccess)
            {
                this.AppContext.WindowManager.Shell.TryInvoke(() =>
                {
                    this.AppContext.WindowManager.Shell.TipShow.ShowMessageAsync(
                        $"监控异常：0x{result.StatusCode:X8} {result.Message}",
                        Symbol.Message
                    );
                });
            }
        }
        catch (OperationCanceledException) when (token.Token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            this.AppContext.WindowManager.Shell.TryInvoke(() =>
            {
                this.AppContext.WindowManager.Shell.TipShow.ShowMessageAsync(
                    $"{exception.Message}",
                    Symbol.Message
                );
            });
        }
    }
}
