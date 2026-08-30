using System;
using System.Collections.Generic;
using System.Text;
using ABI.Models;
using ABIRuntime.Abstractions;

namespace Haiyu.ViewModel.ToolkitsViewModel;

public sealed partial class MonitorToolViewModel : ViewModelBase
{
    private CancellationTokenSource? _monitorCancellation;

    public MonitorToolViewModel(ABIRuntimeService aBIRuntimeService, ITipShow tipShow)
    {
        ABIRuntimeService = aBIRuntimeService;
        TipShow = tipShow;
    }

    public ABIRuntimeService ABIRuntimeService { get; }
    public ITipShow TipShow { get; }

    Progress<IPrivilegedProgress<CMonitorProgress>>? _progress = null;
    Task? _monitorTask = null;

    [ObservableProperty]
    public partial string MonitorText { get; set; } = "等待监控数据";

    public Window? Window { get; internal set; }

    [RelayCommand]
    async Task Loaded()
    {
        if (_monitorTask is { IsCompleted: false })
            return;
        _monitorCancellation?.Dispose();
        _monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.CTS.Token);
        _progress = new Progress<IPrivilegedProgress<CMonitorProgress>>(
            (s) =>
            {
                if (s.Stage == PrivilegedStage.Executing && s.Data != null && s.Data.data != null)
                {
                    CMonitorProgressData data = s.Data.data;
                    Debug.WriteLine(
                        $"FPS Progress: {data.ForgroundProgramName}, {data.FOrgroundProgramFps}"
                    );

                    MonitorText = $"{data.ForgroundProgramName}  {data.FOrgroundProgramFps} FPS";
                }
            }
        );
        _monitorTask = Task.Run(() => MonitorAsync(_progress, _monitorCancellation));
        try
        {
            _ = Task.Run(() => _monitorTask);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task MonitorAsync(
        Progress<IPrivilegedProgress<CMonitorProgress>> progress,
        CancellationTokenSource token
    )
    {
        try
        {
            var initializeTask = await ABIRuntimeService.Initialize(AppDomain.CurrentDomain.BaseDirectory);
            if(!initializeTask)
            {
                Debug.WriteLine("FPS 监控初始化失败。");
                return;
            }
            if (ABIRuntimeService.Runtime == null)
                return;
            IPrivilegedResult<RunResult> result = await ABIRuntimeService.Runtime!.InvokeAsync(
                ABIRuntime.Contract.ComputerMonitorContract,
                new CMonitorRequest(),
                progress,
                token.Token
            );

            if (!result.IsSuccess)
            {
                Debug.WriteLine($"FPS 监控失败：0x{result.StatusCode:X8} {result.Message}");
            }
        }
        catch (OperationCanceledException) when (token.Token.IsCancellationRequested)
        {
            Debug.WriteLine("FPS 监控已取消。");
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"FPS 监控异常：{exception}");
        }
    }

    protected override void OnDisposing()
    {
        _monitorCancellation?.Cancel();
        _monitorCancellation?.Dispose();
        _monitorCancellation = null;
        _progress = null;
        Window = null;

        if (_monitorTask is { IsCompleted: false } monitorTask)
        {
            _ = monitorTask.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted
                    | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
        }

        _monitorTask = null;
        base.OnDisposing();
    }
}
