using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Waves.Api.Models.Enums;
using Windows.ApplicationModel.VoiceCommands;

namespace Haiyu.ViewModel;

partial class WavesAnalysisRecordViewModel
{
    [ObservableProperty]
    public partial ObservableCollection<GameRecordNavigationItem> NavItems { get; set; }

    [ObservableProperty]
    public partial GameRecordNavigationItem SelectNavItem { get; set; }

    /// <summary>
    /// 小保底歪率集合
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<GuaranteRangeWrapper> GuaranteItems { get; set; }

    [ObservableProperty]
    public partial GuaranteRangeWrapper SelectGuarante { get; set; }

    [ObservableProperty]
    public partial double StarAvgValue { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<RecordActivityFiveStarItemWrapper> StarItems { get; set; }

    [ObservableProperty]
    public partial RecordHealderWrapper Header { get; set; }

    public void InitNavItems()
    {
        if (NavItems == null)
            NavItems = new();
        NavItems.Clear();
        foreach (var item in this.Cards.Items)
        {
            this.NavItems.Add(item.GetRecordNavItem());
        }
        SelectNavItem = NavItems[0];
    }

    public async Task AnalysisStarAsync()
    {
        if (GuaranteItems == null)
            GuaranteItems = new();
        GuaranteItems.Clear();
        var upIds = RecordHelper.FormatFiveRoleStar(FiveGroup);
        var allResources = Cards
            .Items.SelectMany(x => x.Resource ?? Enumerable.Empty<RecordCardItemWrapper>())
            .ToList();
        var allStarTotal = allResources.Count(x => x.QualityLevel == 5);
        var crookedTotal = 0;
        //小保底扇形图
        foreach (var item in this.Cards.Items.Where(x => x.IsFlage()))
        {
            var roleRange = RecordHelper.FormatStartFive(item.Resource, out var lastCount, upIds);
            ArgumentNullException.ThrowIfNull(roleRange.Item1);
            var data = RecordHelper.GetGuaranteedRange(roleRange.Item1);

            var flaggedFiveStars = roleRange.Item1;
            int consecutiveLoss = 0;
            for (int i = flaggedFiveStars.Count - 1; i >= 0; i--)
            {
                if (flaggedFiveStars[i].Item3 == true)
                    consecutiveLoss++;
                else if (flaggedFiveStars[i].Item3.HasValue)
                    break;
            }

            bool isNextSmallGuarantee = true;
            foreach (var entry in flaggedFiveStars)
            {
                if (entry.Item3.HasValue)
                    isNextSmallGuarantee = !entry.Item3.Value;
            }

            GuaranteItems.Add(
                new()
                {
                    NG = data,
                    OK = 100 - data,
                    DisplayName = item.GetRecordNavItem().DisplayName,
                    ConsecutiveLoss = consecutiveLoss,
                    GuaranteeStatus = isNextSmallGuarantee ? "小保底" : "大保底",
                }
            );
            this.SelectGuarante = GuaranteItems[0];
            crookedTotal += flaggedFiveStars.Count(x => x.Item3 == true);
        }
        //称号
        var result = Cards.EvaluateLuck(upIds);
        //平均出金抽数
        var fiveStarRecords = RecordHelper.FormatRecordFive(allResources);
        double avgPulls =
            fiveStarRecords.Count > 0 ? Math.Round(fiveStarRecords.CalculateAvg(), 1) : 0;
        //实际出金率
        double actualRate =
            allResources.Count > 0
                ? Math.Round((double)allStarTotal / allResources.Count * 100, 2)
                : 0;
        //抽卡跨度天数
        var validDates = allResources.Select(x => x.RecordTime).Where(x => x != default).ToList();
        int days = validDates.Count > 1 ? (validDates.Max() - validDates.Min()).Days : 0;
        Header = new RecordHealderWrapper()
        {
            Designation = result.Title,
            DoubleCount = (int)result.doubleCount,
            AllTotal = allResources.Count,
            AllStarTotal = allStarTotal,
            CrookedTotal = crookedTotal,
            Score = result.Score,
            AvgPulls = avgPulls,
            ActualFiveStarRate = actualRate,
            Days = days,
        };

        //各卡池抽数分布
        var poolChart = new ObservableCollection<PieSeries>();
        foreach (var item in Cards.Items)
        {
            var resourceCount = item.Resource?.Count() ?? 0;
            if (resourceCount > 0)
            {
                poolChart.Add(
                    new PieSeries()
                    {
                        Name = item.GetRecordNavItem().DisplayName,
                        Value = resourceCount,
                    }
                );
            }
        }
        PoolChart = poolChart;

        //出货占比饼图
        var fourStarTotal = allResources.Count(x => x.QualityLevel == 4);
        StarRatioChart = new ObservableCollection<PieSeries>()
        {
            new PieSeries() { Name = "4星", Value = fourStarTotal },
            new PieSeries() { Name = "5星", Value = allStarTotal },
        };

        if (TimeLineChart == null)
            TimeLineChart = new();
        TimeLineChart.Clear();
        //每日抽数柱状图
        var timeLine = Cards.GetTimeLine();
        foreach (var point in timeLine)
        {
            if (point.DateTime == DateTime.MinValue)
                continue;
            TimeLineChart.Add(new DateTimeChartPoint(point.DateTime, point.Values));
        }
    }

    partial void OnSelectGuaranteChanged(GuaranteRangeWrapper value)
    {
        if (value == null)
            return;
        if (this.GuaranteeChart == null)
            GuaranteeChart = new();
        GuaranteeChart.Clear();
        this.GuaranteeChart = new ObservableCollection<PieSeries>()
        {
            new PieSeries() { Name = "中", Value = value.OK },
            new PieSeries() { Name = "歪", Value = value.NG },
        };
        GuaranteeHeader = $"保底状态：{value.GuaranteeStatus}";
    }

    partial void OnSelectNavItemChanged(GameRecordNavigationItem value)
    {
        if (value == null)
            return;
        try
        {
            var resources = this.Cards.Items.Where(x => x.PoolType == value.Id).FirstOrDefault();
            ArgumentNullException.ThrowIfNull(resources);
            ArgumentNullException.ThrowIfNull(FiveGroup);
            var temp1 = RecordHelper
                .FormatStartFive(
                    resources.Resource,
                    out var lastCount,
                    RecordHelper.FormatFiveRoleStar(this.FiveGroup)
                )
                .Item1;
            ArgumentNullException.ThrowIfNull(temp1);
            var isWeaponPool =
                value.Id == (int)CardPoolType.WeaponsActivity
                || value.Id == (int)CardPoolType.WeaponsResident
                || value.Id == (int)CardPoolType.WeaponNovice
                || value.Id == (int)CardPoolType.WeaponCollaboration;
            StarItems = isWeaponPool
                ? temp1.Format(this.AllWeapon, false).Reverse().ToObservableCollection()
                : temp1.Format(this.AllRole, true).Reverse().ToObservableCollection();
            StarItems.Insert(
                0,
                new RecordActivityFiveStarItemWrapper()
                {
                    Icon = null,
                    Flage = false,
                    Count = lastCount,
                    ShowFlage = Visibility.Collapsed,
                    Name = $"已经垫了{lastCount}发",
                }
            );
            StarAvgValue = Math.Round(StarItems.Select(x => x.Count).Average(), 2);
        }
        catch (Exception)
        {
            return;
        }
    }
}
