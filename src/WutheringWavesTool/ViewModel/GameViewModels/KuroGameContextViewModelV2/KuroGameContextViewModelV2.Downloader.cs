using Waves.Core.Models.Enums;

namespace Haiyu.ViewModel.GameViewModels;

partial class KuroGameContextViewModelV2
{
    
    #region 进度显示

    [ObservableProperty]
    public partial ObservableCollection<DownloadActiveFileItem> ActiveFilesItems { get; set; } = new();

    [ObservableProperty]
    public partial string CurrentStepText { get; set; }

    [ObservableProperty]
    public partial int MaxStep { get; set; }

    [ObservableProperty]
    public partial int CurrentStep { get; set; }

    [ObservableProperty]
    public partial string SpeedText { get; set; }

    [ObservableProperty]
    public partial string ActiveFile { get; set; }

    [ObservableProperty]
    public partial double MaxProgressValue { get; set; }

    [ObservableProperty]
    public partial double CurrentProgressValue { get; set; }

    [ObservableProperty]
    public partial int DownloadSpeedValue { get; set; }

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial string CurrentByteText { get; set; }
    [ObservableProperty]
    public partial string MaxByteText { get; set; }

    [ObservableProperty]
    public partial GameContextActionType CurrentActiveType { get; set; }
    #endregion


    #region 进度图表
    [ObservableProperty]
    public partial ObservableCollection<DateTimeChartPoint> DownloadSpeedPoints { get; set; } = new();
    [ObservableProperty]
    public partial ObservableCollection<DateTimeChartPoint> VerifySpeedPoints { get; set; } = new();
    [ObservableProperty]
    public partial ObservableCollection<DateTimeChartPoint> DecompressSpeedPoints { get; set; } = new();

    public ObservableCollection<ChartSeries> TransferChartSeries { get; private set; } = [];

    public ObservableCollection<ChartAxis> TransferChartXAxes { get; private set; } = [];
    public ObservableCollection<ChartAxis> TransferChartYAxes { get; private set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<double> DownloadSpeedSeparators { get; set; } = GetSeparators();

    private static ObservableCollection<double> GetSeparators()
    {
        var now = DateTime.Now;
        return
        [
            now.AddSeconds(-5).Ticks,
            now.AddSeconds(-3).Ticks,
            now.AddSeconds(-2).Ticks,
            now.AddSeconds(-1).Ticks,
            now.Ticks
        ];
    }

    private static void UpdateSeparators(ObservableCollection<double> separators, DateTime now)
    {
        double[] values =
        [
            now.AddSeconds(-5).Ticks,
            now.AddSeconds(-3).Ticks,
            now.AddSeconds(-2).Ticks,
            now.AddSeconds(-1).Ticks,
            now.Ticks
        ];

        while (separators.Count < values.Length)
        {
            separators.Add(values[separators.Count]);
        }

        for (var i = 0; i < values.Length; i++)
        {
            separators[i] = values[i];
        }

        while (separators.Count > values.Length)
        {
            separators.RemoveAt(separators.Count - 1);
        }
    }


    [ObservableProperty]
    public partial Func<DateTime, string> LabelsFormatter { get; set; } = Formatter;

    public Func<double, string> DataLabelFormatter => value => $"{value:N0}mb/s";

    private void InitializeTransferChart()
    {
        TransferChartSeries =
        [
            CreateSpeedSeries(LanguageService.GetStringByText("下载"), DownloadSpeedPoints, Color.FromArgb(255, 0, 142, 255), 1),
            CreateSpeedSeries(LanguageService.GetStringByText("校验"), VerifySpeedPoints, Color.FromArgb(255, 128, 0, 210), 0),
            CreateSpeedSeries(LanguageService.GetStringByText("解压"), DecompressSpeedPoints, Color.FromArgb(255, 60, 183, 0), 2),
        ];
        TransferChartXAxes = [new DateTimeAxis { DateFormatter = Formatter, Interval = TimeSpan.FromSeconds(1) }];
        TransferChartYAxes =
        [
            new NumericAxis { ShowLabels = false, ShowSeparatorLines = false },
            new NumericAxis { ShowLabels = false, ShowSeparatorLines = false },
            new NumericAxis { ShowLabels = false, ShowSeparatorLines = false },
        ];
    }

    private LineSeries CreateSpeedSeries(string name, IReadOnlyList<DateTimeChartPoint> values, Color color, int axis) =>
        new()
        {
            Name = name,
            Values = values,
            YAxisIndex = axis,
            Stroke = new SolidChartPaint(color, 5),
            Fill = new LinearGradientChartPaint(Color.FromArgb(128, color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B)),
            TooltipFormatter = DataLabelFormatter,
        };

    private static string Formatter(DateTime date)
    {
        return DisplayTimeFormatter.FormatDuration(DateTime.Now - date);
    }
    #endregion

    #region 通知

    #endregion

    [RelayCommand]
    async Task PauseDownloadTask()
    {
        var status = await this.GameContext.GetGameContextStatusAsync(this.CTS.Token);
        if (status.IsPause)
        {
            if (await this.GameContext.ResumeDownloadAsync())
            {
                this.PauseIcon = "\uE769";
            }
        }
        else
        {
            if (await this.GameContext.PauseDownloadAsync())
            {
                this.PauseIcon = "\uE768";
            }
        }
    }

    [RelayCommand]
    async Task CancelDownloadTask()
    {
        await GameContext.StopCannelTaskAsync();
        var status = await GameContext.GetGameContextStatusAsync();
        if (!status.IsLauncher)
        {
            await this.GameContext.GameLocalConfig.SaveConfigAsync(
                GameLocalSettingName.GameLauncherBassFolder,
                ""
            );
            await this.GameContext.GameLocalConfig.SaveConfigAsync(
                GameLocalSettingName.GameLauncherBassProgram,
                ""
            );
            await this.GameContext.GameLocalConfig.SaveConfigAsync(
                GameLocalSettingName.LocalGameUpdateing,
                "False"
            );
        }
        this.ProgressState_OnProgressChanged(this.GameContext.ProgressState);
        this.ProgressValue = 0;
        this.PreProgress = 0;
        this.PreDownloadProgress = 0;
        this.CurrentProgressValue = 0;
        this.GameContext.SystemEventPublisher.Publish(new()
        {
            Message = LanguageService.FormatByText(LanguageService.GetStringByText("取消下载成功")),
            Delay = 5
        });
    }

    [RelayCommand]
    async Task SetDownloadSpeedAsync()
    {
        await GameContext.SetDownloadSpeedAsync(DownloadSpeedValue);
    }

}
