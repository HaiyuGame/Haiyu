using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using ScoringService;
using ScoringService.Models;
using ScoringService.Models.Enums;

namespace Haiyu.Model;

/// <summary>模板的界面副本；选中状态不写入 JSON。</summary>
public sealed partial class SortingTemplateCard(EntryGroup model) : ObservableObject
{
    public EntryGroup Model { get; } = model;
    public string Name => Model.Name;
    public string Caption => Model.Id.StartsWith("builtin-", StringComparison.Ordinal) ? Model.SourceUrl.Length == 0 ? "自定义起点" : "内置参考" : "我的模板";
    [ObservableProperty]
    [JsonIgnore]
    public partial bool IsSelect { get; set; }
    public override string ToString() => Name;
}

/// <summary>词条库的紧凑展示行。</summary>
public sealed partial class SortingEntryCard(EntryItem model)
{
    public EntryItem Model { get; } = model;
    public string Name => Model.EntryName;
    public string Identity => $"{Model.EntryNo} · {(Model.Type == EntryType.Percentage ? "百分比" : "固定值")}";
    public string Summary => Model.Usage == EntryUsage.DevelopmentTarget
        ? $"养成目标 {SortingText.Comparison(Model.Comparison)} {Model.TargetValue}"
        : $"{SortingText.Usage(Model.Usage)} · 基准 {Model.ReferenceValue} · 权重 {Model.Weight ?? 0} · 惩罚 {Model.PenaltyCoefficient ?? 0}";
    public string DisplayText => $"{Name} · {Identity} · {Summary}";
}

/// <summary>单件声骸展示结果。</summary>
public sealed partial class SortingEchoCard(string name, EchoData echo, EchoScoreResult result, string iconUrl = "", ScoreGradeThresholds? thresholds = null, decimal? theoreticalScore = null, decimal? progress = null)
{
    public string IconUrl => iconUrl;
    public string Name => name;
    public string Meta => $"COST {echo.Cost} · +{echo.Level}";
    public string Score => decimal.Round(result.TotalScore, 1, MidpointRounding.AwayFromZero).ToString("F1");
    public double ScoreValue => (double)result.TotalScore;
    public decimal RawScore => result.TotalScore;
    public string Summary => $"主词条 {result.MainStats.Score:F1} · 副词条 {result.SubStats.Score:F1}";
    public string HeaderSummary => $"{Meta} · {Summary}";
    public string Grade => thresholds is null ? ScoreRatings.Grade(result.TotalScore) : ScoreRatings.Grade(result.TotalScore, thresholds);
    public string Comment => (thresholds is null ? ScoreRatings.Comment(result.TotalScore) : ScoreRatings.Comment(result.TotalScore, thresholds)) +
        (theoreticalScore.HasValue ? $" · 理论质量 {theoreticalScore:F1}分" : "") +
        (progress > 100 ? $" · 毕业参考进度 {progress:F1}%" : "");
    public int EffectiveCount => result.SubStats.Details.Count(x => x.Benefit > 0);
    public int SubStatCount => result.SubStats.Details.Count;
    public string HitSummary => $"有效词条 {EffectiveCount}/{SubStatCount}";
    public System.Collections.ObjectModel.ObservableCollection<SortingStatDetailCard> StatDetails { get; } = new(
        result.MainStats.Details.Select(x => new SortingStatDetailCard(x, result.MainStats.MaximumBenefit, "主词条"))
            .Concat(result.SubStats.Details.Select(x => new SortingStatDetailCard(x, result.SubStats.MaximumBenefit, "副词条"))));
    public string Details => string.Join(Environment.NewLine, new[]
    {
        $"主词条 · {result.MainStats.Score:F2}/100",
        Lines(result.MainStats), $"副词条 · {result.SubStats.Score:F2}/100", Lines(result.SubStats)
    });
    public string CopyText => $"{Name} · {Meta} · {Score}分 · {Grade} · {HitSummary}{Environment.NewLine}{Comment}{Environment.NewLine}{Details}";
    private static string Lines(StatScoreResult section) => string.Join(Environment.NewLine, section.Details.Select(x =>
        $"{EchoStatCatalog.Find(x.EntryNo, x.Type)?.Name ?? x.EntryNo} {x.Value}{(x.Type == EntryType.Percentage ? "%" : "")} · { (section.MaximumBenefit > 0 ? x.Benefit / section.MaximumBenefit * 100 : 0):F2}分"));
}

/// <summary>逐条评分展示。是否有效按实际正贡献判断，不把仅匹配配置误报为有效。</summary>
public sealed partial class SortingStatDetailCard(EntryScoreDetail detail, decimal maximum, string usage)
{
    public string Name => EchoStatCatalog.Find(detail.EntryNo, detail.Type)?.Name ?? detail.EntryNo;
    public string Value => $"{detail.Value}{(detail.Type == EntryType.Percentage ? "%" : "")}";
    public string Usage => usage;
    public bool IsEffective => detail.Benefit > 0;
    public string Status => IsEffective ? "有效" : "无收益";
    public string Contribution => $"{(maximum > 0 ? detail.Benefit / maximum * 100 : 0):F2}分";
}

/// <summary>WinUI 词条选择项，计算库不承担 WinRT 投影。</summary>
public sealed partial class SortingStatOption(EchoStatDefinition definition)
{
    public EchoStatDefinition Definition { get; } = definition;
    public string Id => Definition.Id;
    public string Label => Definition.Label;
}

internal static class SortingText
{
    internal static EntryGroup Copy(EntryGroup group) => JsonSerializer.Deserialize(JsonSerializer.Serialize(group, ScoringJsonContext.Default.EntryGroup), ScoringJsonContext.Default.EntryGroup)!;
    internal static EntryItem Copy(EntryItem item) => JsonSerializer.Deserialize(JsonSerializer.Serialize(item, ScoringJsonContext.Default.EntryItem), ScoringJsonContext.Default.EntryItem)!;
    internal static string Usage(EntryUsage usage) => usage switch { EntryUsage.MainStatScoring => "主词条", EntryUsage.SubStatScoring => "副词条", _ => "养成目标" };
    internal static string Comparison(ComparisonOperator? comparison) => comparison switch
    { ComparisonOperator.GreaterThan => ">", ComparisonOperator.GreaterThanOrEqual => "≥", ComparisonOperator.LessThan => "<", ComparisonOperator.LessThanOrEqual => "≤", _ => "=" };
}
