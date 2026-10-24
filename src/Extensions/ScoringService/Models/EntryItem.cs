using System.Text.Json.Serialization;
using ScoringService.Models.Enums;

namespace ScoringService.Models;

/// <summary>模板规则。同一属性可以分别配置主词条、副词条和养成目标。</summary>
public sealed class EntryItem
{
    /// <summary>属性稳定编号，与属性类型共同标识属性，不使用展示名称匹配。</summary>
    [JsonPropertyName("entryNo")]
    public string EntryNo { get; set; } = "";
    /// <summary>属性展示名称，例如攻击力加成、共鸣效率。</summary>
    [JsonPropertyName("entryName")]
    public string EntryName { get; set; } = "";
    /// <summary>属性数值类型：百分比或固定数值。</summary>
    [JsonPropertyName("type")]
    public EntryType Type { get; set; }

    /// <summary>规则用途，决定使用评分参数还是养成目标参数。</summary>
    [JsonPropertyName("usage")]
    public EntryUsage Usage { get; set; }

    /// <summary>旧版评分基准。新版从内置配置读取最大值，不使用本字段作为分母。</summary>
    [JsonPropertyName("referenceValue")]
    public decimal? ReferenceValue { get; set; }

    /// <summary>当前角色模板独立的评分满收益基准；空值沿用游戏满档。百分比以百分点保存，例如 8% 存为 8。此值不改变游戏合法上限。</summary>
    [JsonPropertyName("normalizationReference")]
    public decimal? NormalizationReference { get; set; }

    /// <summary>折算系数，新版为 0～1。零为无效，较低值为次要，1 为最高优先级；不参与扣分。</summary>
    [JsonPropertyName("weight")]
    public decimal? Weight { get; set; }

    /// <summary>旧版惩罚参数；新版只接受空或零，非零旧参数需要用户显式迁移。</summary>
    [JsonPropertyName("penaltyCoefficient")]
    public decimal? PenaltyCoefficient { get; set; }

    /// <summary>养成目标值，非负；仅用于养成规则，260% 存为 260。</summary>
    [JsonPropertyName("targetValue")]
    public decimal? TargetValue { get; set; }

    /// <summary>养成目标比较方式，例如严格大于或大于等于；仅用于养成规则。</summary>
    [JsonPropertyName("comparison")]
    public ComparisonOperator? Comparison { get; set; }
}
