using System;
using System.Collections.Generic;
using System.Text;
namespace Haiyu.ViewModel;

partial class WavesAnalysisRecordViewModel
{
    #region 小保底歪率饼图
    [ObservableProperty]
    public partial string GuaranteeHeader { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<PieSeries> GuaranteeChart { get; set; }
    #endregion

    #region 出货占比饼图
    [ObservableProperty]
    public partial ObservableCollection<PieSeries> StarRatioChart { get; set; }
    #endregion

    #region 各卡池抽数饼图
    [ObservableProperty]
    public partial ObservableCollection<PieSeries> PoolChart { get; set; }
    #endregion

    #region 每日抽数柱状图
    [ObservableProperty]
    public partial ObservableCollection<DateTimeChartPoint> TimeLineChart { get; set; }

    public ObservableCollection<ChartSeries> TimeLineSeries { get; private set; } = [];
    public ObservableCollection<ChartAxis> TimeLineXAxes { get; private set; } = [];
    public ObservableCollection<ChartAxis> TimeLineYAxes { get; private set; } = [new NumericAxis()];

    private void InitializeCharts()
    {
        TimeLineChart ??= [];
        TimeLineSeries = [new ColumnSeries { Name = "每日抽数", Values = TimeLineChart }];
        TimeLineXAxes = [new DateTimeAxis { DateFormatter = Formatter }];
    }

    public Func<DateTime, string> TimeLineFormatter { get; } =
        date => date.ToString("MM/dd");
    #endregion

    public Func<DateTime, string> Formatter { get; set; } =
        date => date.ToString("yyyy-MM-dd");
}
