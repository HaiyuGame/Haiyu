using ABI.Models;
using ABIRuntime.Abstractions;
using Waves.Core.Services;
using Waves.Settings.Models;

namespace Haiyu.ViewModel.ToolkitsViewModel;

/*
 主题
    文字颜色
    字体大小
    字体家族
    项目间距
 窗口行为
    穿透窗口
    显示位置（左上，上居中，右上，左下，下居中，右下）
 CPU子项
    占用率
    温度
    电压
    频率
 GPU子项
    占用率
    温度
    电压
    频率
    Max VRAM Total
    VRAM Used
 内存
    Max Total
    Memory Used
 */

public sealed partial class MonitorToolViewModel : ViewModelBase
{
    private CancellationTokenSource? _monitorCancellation;
    private readonly MoniterSettings _settings = new(
        Path.Combine(AppSettings.BassFolder, "MonitorSettings.json")
    );
    private Window? _window;
    private CPUUIConfig _cpuConfig = new()
    {
        LoadEnable = true, ClockEnable = true, TempateEnable = true, VoltagesEnable = true,
    };
    private GPUUIConfig _gpuConfig = new()
    {
        LoadEnable = true, ClockEnable = true, TempateEnable = true,
        VoltagesEnable = true, VMemoryEnable = true,
    };

    public MonitorToolViewModel(
        IAppContext<App> appContext,
        SystemEventPublisher systemEventPublisher
    )
    {
        this.AppContext = appContext;
        SystemEventPublisher = systemEventPublisher;
        WeakReferenceMessenger.Default.Register<MonitorSettingsChangedMessage>(
            this,
            static (recipient, message) =>
                ((MonitorToolViewModel)recipient).ReceiveSettings(message)
        );
    }

    public IAppContext<App> AppContext { get; }
    public SystemEventPublisher SystemEventPublisher { get; }

    Progress<IPrivilegedProgress<CMonitorProgress>>? _monitorProgress = null;

    Progress<IPrivilegedProgress<FpsMonitorProgress>>? _monitorFpsProgress = null;

    Task? _monitorTask = null;

    Task? _fpsTask = null;

    #region 监控数据

    [ObservableProperty]
    public partial int FPS { get; set; }

    [ObservableProperty]
    public partial string ForegroundProgramName { get; set; } = "--";

    [ObservableProperty]
    public partial double FrameTime { get; set; }

    [ObservableProperty]
    public partial double AverageFPS { get; set; }

    [ObservableProperty]
    public partial double Low1PercentFPS { get; set; }

    [ObservableProperty]
    public partial double Low01PercentFPS { get; set; }

    [ObservableProperty]
    public partial double P99FrameTime { get; set; }

    [ObservableProperty]
    public partial double MaxFrameTime { get; set; }

    [ObservableProperty]
    public partial int MaximumFPS { get; set; }

    [ObservableProperty]
    public partial string MemoryText { get; set; } = "--";

    [ObservableProperty]
    public partial double Power { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<MonitorDeviceItem> CPUS { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<MonitorDeviceItem> GPUS { get; set; } = [];

    #endregion


    [ObservableProperty] public partial bool CurrentFpsEnable { get; set; } = true;
    [ObservableProperty] public partial bool FPS1LowEnable { get; set; }
    [ObservableProperty] public partial bool FPS01LowEnable { get; set; } = true;
    [ObservableProperty] public partial bool FPSMaxEnable { get; set; }
    [ObservableProperty] public partial bool MaxFrameTimeEnable { get; set; } = true;
    [ObservableProperty] public partial bool PowerEnable { get; set; }
    [ObservableProperty] public partial bool MemoryEnable { get; set; }
    [ObservableProperty] public partial double MonitorFontSize { get; set; } = 14;
    [ObservableProperty] public partial double MonitorItemSpacing { get; set; } = 5;
    [ObservableProperty] public partial SolidColorBrush MonitorForeground { get; set; } = new(Colors.White);
    [ObservableProperty] public partial FontFamily MonitorFontFamily { get; set; } = new("微软雅黑");

    public Window? Window
    {
        get => _window;
        internal set
        {
            _window = value;
            ApplyClickThrough(_isClickThrough);
        }
    }

    private bool _isClickThrough = true;

    private void ReceiveSettings(MonitorSettingsChangedMessage message)
    {
        Window? window = Window;
        if (window is null || window.DispatcherQueue.HasThreadAccess)
        {
            ApplySettings(message);
            return;
        }

        window.DispatcherQueue.TryEnqueue(() =>
        {
            if (IsAlive)
                ApplySettings(message);
        });
    }

    private void ApplySettings(MonitorSettingsChangedMessage message)
    {
        MonitorFontSize = Math.Clamp(message.Theme.FontSize, 10, 32);
        MonitorItemSpacing = Math.Clamp(message.Theme.MonitorItemSpacing, 0, 30);
        MonitorForeground = new SolidColorBrush(ParseColor(message.Theme.FontColor));
        MonitorFontFamily = new FontFamily(
            string.IsNullOrWhiteSpace(message.Theme.FontFamily) ? "微软雅黑" : message.Theme.FontFamily
        );
        CurrentFpsEnable = message.FPS.CurrentFpsEnable;
        FPS1LowEnable = message.FPS.FPS1LowEnable;
        FPS01LowEnable = message.FPS.FPS01LowEnable;
        FPSMaxEnable = message.FPS.FPSMaxEnable;
        MaxFrameTimeEnable = message.FPS.MaxFrameTimeEnable;
        PowerEnable = message.Power.Enable;
        MemoryEnable = message.Memory.Enable;
        _cpuConfig = message.CPU;
        _gpuConfig = message.GPU;
        _isClickThrough = message.Theme.IsAllowDesktop;

        foreach (MonitorDeviceItem item in CPUS)
            item.Apply(message.CPU);
        foreach (MonitorDeviceItem item in GPUS)
            item.Apply(message.GPU);

        ApplyClickThrough(_isClickThrough);
    }

    private static Color ParseColor(string? value)
    {
        string hex = value?.Trim().TrimStart('#') ?? string.Empty;
        if (hex.Length == 6)
            hex = "FF" + hex;
        return uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint argb)
            ? Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb)
            : Colors.White;
    }

    private async Task LoadSettingsAsync()
    {
        MoniterTheme theme = await _settings.GetMoniterThemeAsync(LifetimeToken) ?? new();
        FPSUIConfig fps = await _settings.GetMoniterFPSAsync(LifetimeToken) ?? new()
        {
            CurrentFpsEnable = true,
            FPS01LowEnable = true,
            MaxFrameTimeEnable = true,
        };
        CPUUIConfig cpu = await _settings.GetMoniterCPUAsync(LifetimeToken) ?? new()
        {
            LoadEnable = true, ClockEnable = true, TempateEnable = true, VoltagesEnable = true,
        };
        GPUUIConfig gpu = await _settings.GetMoniterGPUAsync(LifetimeToken) ?? new()
        {
            LoadEnable = true, ClockEnable = true, TempateEnable = true,
            VoltagesEnable = true, VMemoryEnable = true,
        };
        MoniterUIItem power = await _settings.GetMoniterPowerAsync(LifetimeToken) ?? new();
        MoniterUIItem memory = await _settings.GetMoniterMemoryAsync(LifetimeToken) ?? new();
        ApplySettings(new(theme, fps, cpu, gpu, power, memory));
    }

    private void ApplyClickThrough(bool isClickThrough)
    {
        Window? window = Window;
        if (window is not TransparentWindowBase transparentWindow)
            return;

        if (window.DispatcherQueue.HasThreadAccess)
        {
            transparentWindow.IsClickThrough = isClickThrough;
            return;
        }

        window.DispatcherQueue.TryEnqueue(() => transparentWindow.IsClickThrough = isClickThrough);
    }

    [RelayCommand]
    async Task Loaded()
    {
        if (_monitorTask is { IsCompleted: false } || _fpsTask is { IsCompleted: false })
            return;
        await LoadSettingsAsync();
        _monitorCancellation?.Dispose();
        _monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.CTS.Token);

        bool initialized = await AppContext.ABIRuntimeService.Initialize(
            Waves.Settings.AppSettings.ABIRuntimeSavePath
        );
        if (!initialized || AppContext.ABIRuntimeService.Runtime is null || !IsAlive)
        {
            Debug.WriteLine("监控运行时初始化失败。");
            return;
        }

        _monitorProgress = new Progress<IPrivilegedProgress<CMonitorProgress>>(
            (s) =>
            {
                if (!IsAlive)
                    return;

                try
                {
                    if (s.Stage == PrivilegedStage.Executing && s.Data?.data is { } data)
                    {
                        CPUS = new ObservableCollection<MonitorDeviceItem>(
                            data.Cpus.Select(
                                (cpu, index) =>
                                    new MonitorDeviceItem
                                    {
                                        Index = index + 1,
                                        IndexStr = $"CPU[{index + 1}]",
                                        Tempate = Math.Round(cpu.Temperature, 2),
                                        Load = GetSensorValue(
                                            cpu.Load,
                                            "CPU Total",
                                            "Total CPU Utility"
                                        ),
                                        Voltages = GetSensorValue(
                                            cpu.Voltages,
                                            "CPU Core",
                                            "Vcore"
                                        ),
                                        Clock = GetSensorValue(cpu.Clock, "CPU Core", "Core"),
                                        LoadEnable = _cpuConfig.LoadEnable,
                                        ClockEnable = _cpuConfig.ClockEnable,
                                        TempateEnable = _cpuConfig.TempateEnable,
                                        VoltagesEnable = _cpuConfig.VoltagesEnable,
                                    }
                            )
                        );

                        GPUS = new ObservableCollection<MonitorDeviceItem>(
                            (data.Gpus ?? []).Select(
                                (gpu, index) =>
                                    new MonitorDeviceItem
                                    {
                                        Index = index + 1,
                                        IndexStr = $"GPU[{index + 1}]",
                                        Tempate = GetSensorValue(
                                            gpu.Temperatures,
                                            "GPU Core",
                                            "GPU Package"
                                        ),
                                        Load = GetSensorValue(gpu.Load, "GPU Core", "D3D 3D"),
                                        Voltages = GetSensorValue(gpu.Voltages, "GPU Core"),
                                        Clock = GetSensorValue(gpu.Clock, "GPU Core"),
                                        Memory =
                                            $"{Math.Round(GetSensorValue(gpu.Memory, "GPU Memory Used") / 1024, 2)}G/{Math.Round(GetSensorValue(gpu.Memory, "GPU Memory Total") / 1024, 2)}G",
                                        CurrentVRRAM = Math.Round(
                                            GetSensorValue(gpu.Memory, "GPU Memory Used") / 1024,
                                            2
                                        ),
                                        MaxVRRAM = Math.Round(
                                            GetSensorValue(gpu.Memory, "GPU Memory Total") / 1024,
                                            2
                                        ),
                                        LoadEnable = _gpuConfig.LoadEnable,
                                        ClockEnable = _gpuConfig.ClockEnable,
                                        TempateEnable = _gpuConfig.TempateEnable,
                                        VoltagesEnable = _gpuConfig.VoltagesEnable,
                                        VMemoryEnable = _gpuConfig.VMemoryEnable,
                                    }
                            )
                        );

                        if (data.Memory is { } memory)
                        {
                            MemoryText = $"{Math.Round(memory.Used, 1)}G/{Math.Round(memory.Total, 1)}G";
                        }

                        Power = Math.Round(
                            (data.Gpus ?? []).Sum(gpu =>
                                GetSensorValue(gpu.Power, "GPU Package", "GPU Power")),
                            1
                        );
                    }
                }
                catch (Exception exception)
                {
                    SystemEventPublisher.Publish(
                        new()
                        {
                            Delay = TimeSpan.FromSeconds(20).TotalSeconds,
                            Message = string.Format(LanguageService.GetString("Monitor_ShowHardwareError"), exception.Message),
                        }
                    );
                    Logger.WriteError($"硬件监控异常{exception.Message}{exception.StackTrace}");
                }
            }
        );
        _monitorFpsProgress = new Progress<IPrivilegedProgress<FpsMonitorProgress>>(
            (s) =>
            {
                if (IsAlive && s.Stage == PrivilegedStage.Executing && s.Data?.data is { } data)
                {
                    ForegroundProgramName = data.ForgroundProgramName;
                    FPS = data.FOrgroundProgramFps;
                    MaximumFPS = Math.Max(MaximumFPS, FPS);
                    FrameTime = Math.Round(data.CurrentFrameTime, 2);
                    AverageFPS = Math.Round(data.AverageFps, 1);
                    Low1PercentFPS = Math.Round(data.Low1PercentFps, 1);
                    Low01PercentFPS = Math.Round(data.Low01PercentFps, 1);
                    P99FrameTime = Math.Round(data.FrameTimeP99, 2);
                    MaxFrameTime = Math.Round(data.MaxFrameTime, 2);
                }
            }
        );
        _monitorTask = MonitorAsync(_monitorProgress, _monitorCancellation);
        _fpsTask = FpsMonitorAsync(_monitorFpsProgress, _monitorCancellation);
        _ = ObserveMonitorTaskAsync(_monitorTask);
        _ = ObserveMonitorTaskAsync(_fpsTask);
    }

    private static double GetSensorValue(
        IReadOnlyDictionary<string, double> sensors,
        params string[] preferredNames
    )
    {
        foreach (string preferredName in preferredNames)
        {
            foreach (KeyValuePair<string, double> sensor in sensors)
            {
                if (
                    sensor.Key.Equals(preferredName, StringComparison.OrdinalIgnoreCase)
                    && double.IsFinite(sensor.Value)
                )
                    return Math.Round(sensor.Value, 2);
            }
        }

        foreach (string preferredName in preferredNames)
        {
            foreach (KeyValuePair<string, double> sensor in sensors)
            {
                if (
                    sensor.Key.Contains(preferredName, StringComparison.OrdinalIgnoreCase)
                    && double.IsFinite(sensor.Value)
                )
                    return Math.Round(sensor.Value, 2);
            }
        }

        return Math.Round(sensors.Values.Where(double.IsFinite).DefaultIfEmpty(0d).Max());
    }

    private async Task FpsMonitorAsync(
        Progress<IPrivilegedProgress<FpsMonitorProgress>> progress,
        CancellationTokenSource token
    )
    {
        try
        {
            if (AppContext.ABIRuntimeService.Runtime == null)
                return;
            IPrivilegedResult<RunResult> result =
                await AppContext.ABIRuntimeService.Runtime!.InvokeAsync(
                    ABIRuntime.Contract.FpsMonitorContract,
                    new FpsMonitorRequest(),
                    progress,
                    token.Token
                );

            if (!result.IsSuccess)
            {
                SystemEventPublisher.Publish(
                    new()
                    {
                        Delay = System.TimeSpan.FromSeconds(20).TotalSeconds,
                        Message = $"Device Monitor {result.Message}",
                    }
                );
                Logger.WriteError($"硬件监控异常{result.Message}");
            }
        }
        catch (OperationCanceledException) when (token.Token.IsCancellationRequested)
        {
            Debug.WriteLine("硬件监控已取消。");
        }
        catch (System.Exception exception)
        {
            SystemEventPublisher.Publish(
                new()
                {
                    Delay = TimeSpan.FromSeconds(20).TotalSeconds,
                    Message = $"Device Monitor{exception.Message}",
                }
            );
            Logger.WriteError($"硬件监控异常{exception.Message}{exception.StackTrace}");
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
        _monitorFpsProgress = null;
        Window = null;

        ObserveFaultAfterDispose(_monitorTask);
        ObserveFaultAfterDispose(_fpsTask);

        _monitorTask = null;
        _fpsTask = null;
        base.OnDisposing();
    }

    private static void ObserveFaultAfterDispose(Task? task)
    {
        if (task is not { IsCompleted: false })
            return;

        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted
                | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
    }
}

public sealed partial class MonitorDeviceItem : ObservableObject
{
    [ObservableProperty] public partial bool LoadEnable { get; set; } = true;
    [ObservableProperty] public partial bool ClockEnable { get; set; } = true;
    [ObservableProperty] public partial bool TempateEnable { get; set; } = true;
    [ObservableProperty] public partial bool VoltagesEnable { get; set; } = true;
    [ObservableProperty] public partial bool VMemoryEnable { get; set; } = true;

    public void Apply(CPUUIConfig config)
    {
        LoadEnable = config.LoadEnable;
        ClockEnable = config.ClockEnable;
        TempateEnable = config.TempateEnable;
        VoltagesEnable = config.VoltagesEnable;
    }

    public void Apply(GPUUIConfig config)
    {
        LoadEnable = config.LoadEnable;
        ClockEnable = config.ClockEnable;
        TempateEnable = config.TempateEnable;
        VoltagesEnable = config.VoltagesEnable;
        VMemoryEnable = config.VMemoryEnable;
    }

    [ObservableProperty]
    public partial int Index { get; set; }

    [ObservableProperty]
    public partial string IndexStr { get; set; }

    [ObservableProperty]
    public partial double Tempate { get; set; }

    [ObservableProperty]
    public partial double Load { get; set; }

    [ObservableProperty]
    public partial double Voltages { get; set; }

    [ObservableProperty]
    public partial double Clock { get; set; }

    [ObservableProperty]
    public partial string Memory { get; set; }

    [ObservableProperty]
    public partial double MaxVRRAM { get; set; }

    [ObservableProperty]
    public partial double CurrentVRRAM { get; set; }
}
