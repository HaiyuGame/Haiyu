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

    /// <summary>评分归一化基准，大于零。百分比 10% 存为 10。</summary>
    [JsonPropertyName("referenceValue")]
    public decimal? ReferenceValue { get; set; }

    /// <summary>收益权重，非负；与惩罚系数互斥。</summary>
    [JsonPropertyName("weight")]
    public decimal? Weight { get; set; }

    /// <summary>惩罚系数，非负；按实际数值归一化后参与减法。</summary>
    [JsonPropertyName("penaltyCoefficient")]
    public decimal? PenaltyCoefficient { get; set; }

    /// <summary>养成目标值，非负；仅用于养成规则，260% 存为 260。</summary>
    [JsonPropertyName("targetValue")]
    public decimal? TargetValue { get; set; }

    /// <summary>养成目标比较方式，例如严格大于或大于等于；仅用于养成规则。</summary>
    [JsonPropertyName("comparison")]
    public ComparisonOperator? Comparison { get; set; }
}
