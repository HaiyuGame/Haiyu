using System.Text.Json.Serialization;
using ScoringService.Models.Enums;

namespace ScoringService.Models;

/// <summary>单条实际属性的评分明细，保留原始贡献，不随最终评分截断。</summary>
/// <param name="EntryNo">实际属性编号。</param>
/// <param name="Type">属性数值类型，区分百分比与固定值。</param>
/// <param name="Value">实际属性数值，10% 存为 10。</param>
/// <param name="IsMatched">是否匹配到对应用途的模板规则。</param>
/// <param name="NormalizedValue">实际值除以评分基准值，未匹配规则时为零。</param>
/// <param name="Benefit">归一化值乘以收益权重后的正向贡献。</param>
/// <param name="Penalty">归一化值乘以惩罚系数后的扣减量，以非负数表示。</param>
public sealed record EntryScoreDetail(
    [property: JsonPropertyName("entryNo")] string EntryNo,
    [property: JsonPropertyName("type")] EntryType Type,
    [property: JsonPropertyName("value")] decimal Value,
    [property: JsonPropertyName("isMatched")] bool IsMatched,
    [property: JsonPropertyName("normalizedValue")] decimal NormalizedValue,
    [property: JsonPropertyName("benefit")] decimal Benefit,
    [property: JsonPropertyName("penalty")] decimal Penalty)
{
    /// <summary>该属性的净贡献，等于收益减去惩罚，可为负数。</summary>
    [JsonPropertyName("netContribution")]
    public decimal NetContribution => Benefit - Penalty;
}

/// <summary>主词条或副词条一组属性的百分制评分结果。</summary>
/// <param name="Score">限制在 0～100 范围内的评分。</param>
/// <param name="MaximumBenefit">模板配置的理论最大收益，即百分制换算分母。</param>
/// <param name="Details">每条实际属性的评分明细，包含未匹配规则的属性。</param>
public sealed record StatScoreResult(
    [property: JsonPropertyName("score")] decimal Score,
    [property: JsonPropertyName("maximumBenefit")] decimal MaximumBenefit,
    [property: JsonPropertyName("details")] IReadOnlyList<EntryScoreDetail> Details)
{
    /// <summary>各条属性的正向收益合计，未做百分制换算。</summary>
    [JsonPropertyName("benefit")]
    public decimal Benefit => Details.Sum(x => x.Benefit);

    /// <summary>各条属性的惩罚扣减量合计，以非负数表示，未做百分制换算。</summary>
    [JsonPropertyName("penalty")]
    public decimal Penalty => Details.Sum(x => x.Penalty);
}

/// <summary>单件声骸的主词条、副词条及加权总分。</summary>
/// <param name="EchoId">评分对应的声骸标识。</param>
/// <param name="MainStats">主词条评分结果。</param>
/// <param name="SubStats">副词条评分结果。</param>
/// <param name="TotalScore">按模板主副占比合成的百分制总分。</param>
public sealed record EchoScoreResult(
    [property: JsonPropertyName("echoId")] string EchoId,
    [property: JsonPropertyName("mainStats")] StatScoreResult MainStats,
    [property: JsonPropertyName("subStats")] StatScoreResult SubStats,
    [property: JsonPropertyName("totalScore")] decimal TotalScore);

/// <summary>单项养成目标的检查结果，面板缺失属性时不视为达标。</summary>
/// <param name="EntryNo">目标属性编号。</param>
/// <param name="EntryName">目标属性展示名称。</param>
/// <param name="Type">目标属性数值类型。</param>
/// <param name="ActualValue">角色最终面板值，为空表示面板缺失该属性。</param>
/// <param name="TargetValue">模板配置的养成目标值。</param>
/// <param name="Comparison">实际值与目标值的比较方式。</param>
/// <param name="IsMet">该目标是否达标，缺失属性时为 false。</param>
public sealed record DevelopmentTargetResult(
    [property: JsonPropertyName("entryNo")] string EntryNo,
    [property: JsonPropertyName("entryName")] string EntryName,
    [property: JsonPropertyName("type")] EntryType Type,
    [property: JsonPropertyName("actualValue")] decimal? ActualValue,
    [property: JsonPropertyName("targetValue")] decimal TargetValue,
    [property: JsonPropertyName("comparison")] ComparisonOperator Comparison,
    [property: JsonPropertyName("isMet")] bool IsMet);

/// <summary>角色养成检查汇总，不参与声骸评分。</summary>
/// <param name="Targets">各项养成目标的检查结果。</param>
public sealed record DevelopmentResult(
    [property: JsonPropertyName("targets")] IReadOnlyList<DevelopmentTargetResult> Targets)
{
    /// <summary>模板是否至少配置了一项目标。</summary>
    [JsonPropertyName("isConfigured")]
    public bool IsConfigured => Targets.Count > 0;

    /// <summary>已达标的目标数量。</summary>
    [JsonPropertyName("metCount")]
    public int MetCount => Targets.Count(x => x.IsMet);

    /// <summary>是否已配置目标且全部达标，无目标时为 false。</summary>
    [JsonPropertyName("isComplete")]
    public bool IsComplete => IsConfigured && Targets.All(x => x.IsMet);
}

/// <summary>单件声骸评分与角色最终面板养成检查的联合结果。</summary>
/// <param name="EchoScore">单件声骸评分结果。</param>
/// <param name="Development">独立的角色养成检查结果。</param>
public sealed record CharacterEvaluationResult(
    [property: JsonPropertyName("echoScore")] EchoScoreResult EchoScore,
    [property: JsonPropertyName("development")] DevelopmentResult Development);
