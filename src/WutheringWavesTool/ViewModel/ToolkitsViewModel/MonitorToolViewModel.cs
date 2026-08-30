using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using ABI.Models;
using ABIRuntime.Abstractions;
using ZXing.Aztec.Internal;

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

    Progress<IPrivilegedProgress<CMonitorProgress>>? _monitorProgress = null;

    Progress<IPrivilegedProgress<FpsMonitorProgress>>? _monitorFpsProgress = null;

    Task? _monitorTask = null;

    Task? _fpsTask = null;


    #region 监控数据

    [ObservableProperty]
    public partial int FPS { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<MonitorDeviceItem> CPUS { get; set; } = [];


    [ObservableProperty]
    public partial ObservableCollection<MonitorDeviceItem> GPUS { get; set; } = [];


    #endregion


    public Window? Window { get; internal set; }

    [RelayCommand]
    Task Loaded()
    {
        if (_monitorTask is { IsCompleted: false })
            return Task.CompletedTask;
        _monitorCancellation?.Dispose();
        _monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.CTS.Token);
        _monitorProgress = new Progress<IPrivilegedProgress<CMonitorProgress>>(
            (s) =>
            {
                if (s.Stage == PrivilegedStage.Executing && s.Data != null && s.Data.data != null)
                {
                    MonitorRecord data = s.Data.data;
                    CPUS = new ObservableCollection<MonitorDeviceItem>(
                        data.Cpus.Select((cpu, index) => new MonitorDeviceItem
                        {
                            Index = index+1,
                            Tempate = Math.Round(cpu.Temperature,2),
                            Load = GetSensorValue(cpu.Load, "CPU Total", "Total CPU Utility"),
                            Voltages = GetSensorValue(cpu.Voltages, "CPU Core", "Vcore"),
                            Clock = GetSensorValue(cpu.Clock, "CPU Core", "Core")
                        }));

                    GPUS = new ObservableCollection<MonitorDeviceItem>(
                        (data.Gpus ?? []).Select((gpu, index) => new MonitorDeviceItem
                        {
                            Index = index+1,
                            Tempate = GetSensorValue(gpu.Temperatures, "GPU Core", "GPU Package"),
                            Load = GetSensorValue(gpu.Load, "GPU Core", "D3D 3D"),
                            Voltages = GetSensorValue(gpu.Voltages, "GPU Core"),
                            Clock = GetSensorValue(gpu.Clock, "GPU Core")
                        }));
                }
            }
        );
        _monitorFpsProgress = new Progress<IPrivilegedProgress<FpsMonitorProgress>>((s) =>
        {
            if (s.Stage == PrivilegedStage.Executing && s.Data != null && s.Data.data != null)
            {
                var data = s.Data.data;
                this.FPS = data.FOrgroundProgramFps;
            }
        });
        _monitorTask = MonitorAsync(_monitorProgress, _monitorCancellation);
        _fpsTask = FpsMonitorAsync(_monitorFpsProgress, _monitorCancellation);
        _ = ObserveMonitorTaskAsync(_monitorTask);
        _ = ObserveMonitorTaskAsync(_fpsTask);
        return Task.CompletedTask;
    }

    /// <summary>优先读取设备的主要传感器；名称不匹配时取同类传感器中的最大有效值。</summary>
    private static double GetSensorValue(
        IReadOnlyDictionary<string, double> sensors,
        params string[] preferredNames)
    {
        foreach (string preferredName in preferredNames)
        {
            foreach (KeyValuePair<string, double> sensor in sensors)
            {
                if (sensor.Key.Equals(preferredName, StringComparison.OrdinalIgnoreCase)
                    && double.IsFinite(sensor.Value))
                    return Math.Round( sensor.Value,2);
            }
        }

        foreach (string preferredName in preferredNames)
        {
            foreach (KeyValuePair<string, double> sensor in sensors)
            {
                if (sensor.Key.Contains(preferredName, StringComparison.OrdinalIgnoreCase)
                    && double.IsFinite(sensor.Value))
                    return Math.Round(sensor.Value, 2);
            }
        }

        return Math.Round(sensors.Values.Where(double.IsFinite).DefaultIfEmpty(0d).Max());
    }

    private async Task FpsMonitorAsync(Progress<IPrivilegedProgress<FpsMonitorProgress>> progress, CancellationTokenSource token)
    {
        try
        {
            var initializeTask = await ABIRuntimeService.Initialize(
                AppDomain.CurrentDomain.BaseDirectory
            );
            if (!initializeTask)
            {
                Debug.WriteLine("硬件监控初始化失败。");
                return;
            }
            if (ABIRuntimeService.Runtime == null)
                return;
            IPrivilegedResult<RunResult> result = await ABIRuntimeService.Runtime!.InvokeAsync(
                ABIRuntime.Contract.FpsMonitorContract,
                new FpsMonitorRequest(),
                progress,
                token.Token
            );

            if (!result.IsSuccess)
            {
                Debug.WriteLine($"硬件监控失败：0x{result.StatusCode:X8} {result.Message}");
            }
        }
        catch (OperationCanceledException) when (token.Token.IsCancellationRequested)
        {
            Debug.WriteLine("硬件监控已取消。");
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"硬件监控异常：{exception}");
        }
    }

    private async Task ObserveMonitorTaskAsync(Task monitorTask)
    {
        try
        {
            await monitorTask;
        }
        catch (OperationCanceledException)
            when (_monitorCancellation?.IsCancellationRequested == true) { }
        catch (Exception exception)
        {
            Debug.WriteLine($"硬件监控任务异常：{exception}");
        }
    }

    public async Task MonitorAsync(
        Progress<IPrivilegedProgress<CMonitorProgress>> progress,
        CancellationTokenSource token
    )
    {
        try
        {
            var initializeTask = await ABIRuntimeService.Initialize(
                AppDomain.CurrentDomain.BaseDirectory
            );
            if (!initializeTask)
            {
                Debug.WriteLine("硬件监控初始化失败。");
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
                Debug.WriteLine($"硬件监控失败：0x{result.StatusCode:X8} {result.Message}");
            }
        }
        catch (OperationCanceledException) when (token.Token.IsCancellationRequested)
        {
            Debug.WriteLine("硬件监控已取消。");
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"硬件监控异常：{exception}");
        }
    }

    protected override void OnDisposing()
    {
        _monitorCancellation?.Cancel();
        _monitorCancellation?.Dispose();
        _monitorCancellation = null;
        _monitorProgress = null;
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


public sealed partial class MonitorDeviceItem:ObservableObject
{
    [ObservableProperty]
    public partial int Index { get; set; }


    [ObservableProperty]
    public partial double Tempate { get; set; }


    [ObservableProperty]
    public partial double Load { get; set; }

    [ObservableProperty]
    public partial double Voltages { get; set; }

    [ObservableProperty]
    public partial double Clock { get; set; }
}
