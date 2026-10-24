using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Haiyu.Model;
using ScoringService;
using ScoringService.Models;
using ScoringService.Models.Enums;

namespace Haiyu.ViewModel.SortingViewModels;

/// <summary>一条折算规则；滑块与数字输入共享同一个值。</summary>
public sealed partial class SortingEntryViewModel(EntryItem item) : ObservableObject
{
    public EntryItem Model { get; } = item;
    public string Name => EchoStatCatalog.Find(Model.EntryNo, Model.Type)?.Label ?? Model.EntryName;
    public string Usage => SortingText.Usage(Model.Usage);
    public string SliderId => $"SortingCoefficient-{Model.Usage}-{Model.EntryNo}-{Model.Type}";
    public string NumberId => SliderId + "-Number";
    public string Limit
    {
        get
        {
            var suffix = Model.Type == EntryType.Percentage ? "%" : "";
            if (Model.NormalizationReference is > 0)
                return $"角色基准：实际值 ÷ {Model.NormalizationReference}{suffix} × 收益系数（达标封顶）";
            if (Model.Usage == EntryUsage.SubStatScoring)
                return $"归一化：实际值 ÷ {EchoStatCatalog.Find(Model.EntryNo, Model.Type)?.SubMaximum}{suffix} × 收益系数";
            var limits = new[] { 1, 3, 4 }
                .Select(cost =>
                    (cost, max: EchoStatCatalog.MainMaximum(Model.EntryNo, Model.Type, cost))
                )
                .Where(x => x.max > 0)
                .Select(x => $"{x.cost}C {x.max}{suffix}");
            return "归一化：实际值 ÷ 对应满值 × 收益系数 · " + string.Join(" / ", limits);
        }
    }
    public double Coefficient
    {
        get => (double)(Model.Weight ?? 0);
        set
        {
            if (!double.IsFinite(value))
                return;
            var next = decimal.Round((decimal)Math.Clamp(value, 0, 1), 2);
            if (next == Model.Weight)
                return;
            Model.Weight = next;
            OnPropertyChanged();
        }
    }
}

/// <summary>养成阈值与本次手动输入的面板值；面板值不写进模板。</summary>
public sealed partial class SortingTargetViewModel(EntryItem item) : ObservableObject
{
    public EntryItem Model { get; } = item;
    public string Name => EchoStatCatalog.Find(Model.EntryNo, Model.Type)?.Label ?? Model.EntryName;
    public string Condition => $"{SortingText.Comparison(Model.Comparison)} {Model.TargetValue}";
    public string Operator => SortingText.Comparison(Model.Comparison);
    public double Target
    {
        get => (double)(Model.TargetValue ?? 0);
        set
        {
            if (double.IsFinite(value) && value >= 0)
            {
                Model.TargetValue = (decimal)value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Condition));
            }
        }
    }

    [ObservableProperty]
    public partial double Actual { get; set; } = double.NaN;

    [ObservableProperty]
    public partial string Status { get; set; } = "暂无面板";
    public string ActualText =>
        double.IsFinite(Actual)
            ? $"{Actual:0.##}{(Model.Type == EntryType.Percentage ? "%" : "")}"
            : "—";

    partial void OnActualChanged(double value)
    {
        OnPropertyChanged(nameof(ActualText));
        if (!double.IsFinite(value) || value < 0)
        {
            Status = "暂无面板";
            return;
        }
        var actual = (decimal)value;
        var met = Model.Comparison switch
        {
            ComparisonOperator.GreaterThan => actual > Model.TargetValue,
            ComparisonOperator.LessThan => actual < Model.TargetValue,
            ComparisonOperator.LessThanOrEqual => actual <= Model.TargetValue,
            ComparisonOperator.Equal => actual == Model.TargetValue,
            _ => actual >= Model.TargetValue,
        };
        Status = met ? "已达标" : "未达标";
    }
}
