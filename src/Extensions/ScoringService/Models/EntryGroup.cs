using System.Text.Json.Serialization;

namespace ScoringService.Models;

/// <summary>角色模板。新版分母由内置合法词条池自动计算，固定权重表示静态适配而非实际伤害。</summary>
public sealed class EntryGroup
{
    public EntryGroup() { }

    /// <summary>缺少版本字段的历史 JSON 按旧版读取，不悄悄改变旧模板的语义。</summary>
    [JsonConstructor]
    public EntryGroup(int scoringVersion = 1) => ScoringVersion = scoringVersion;
    /// <summary>算法版本。2 为内置词条上限与合法组合归一化；1 仅用于显式读取旧测试和旧模板。</summary>
    [JsonPropertyName("scoringVersion")]
    public int ScoringVersion { get; set; } = 2;

    /// <summary>毕业参考的有效副词条数量，范围1～5；旧模板缺省为4，不改变旧版ScoreEcho计算。</summary>
    [JsonPropertyName("targetEffectiveStatCount")]
    public int TargetEffectiveStatCount { get; set; } = 4;

    /// <summary>角色模板独立的评级及毕业门槛，使用百分制；与面板养成检查分开。</summary>
    [JsonPropertyName("gradeThresholds")]
    public ScoreGradeThresholds GradeThresholds { get; set; } = new();

    /// <summary>参考资料链接；数字系数是本工具的经验配置，并非来源网站发布的评分公式。</summary>
    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; set; } = "";

    /// <summary>配置备注，包括适用配装和参考日期。</summary>
    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";
    /// <summary>模板唯一编号。</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>模板展示名称，同一角色可以创建多套模板。</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>模板关联的角色编号。</summary>
    [JsonPropertyName("roleId")]
    public string RoleId { get; set; } = "";

    /// <summary>主词条评分、副词条评分和角色养成目标规则，按用途区分。</summary>
    [JsonPropertyName("items")]
    public List<EntryItem> Items { get; set; } = [];

    /// <summary>主词条分在总分中的占比，0.3 表示 30%；与副词条占比之和为 1。</summary>
    [JsonPropertyName("mainStatWeight")]
    public decimal MainStatWeight { get; set; } = 0.3m;

    /// <summary>副词条分在总分中的占比，0.7 表示 70%；与主词条占比之和为 1。</summary>
    [JsonPropertyName("subStatWeight")]
    public decimal SubStatWeight { get; set; } = 0.7m;

    /// <summary>旧版主词条分母；新版忽略此字段，按 COST 自动计算。</summary>
    [JsonPropertyName("mainStatMaximumBenefit")]
    public decimal MainStatMaximumBenefit { get; set; }

    /// <summary>旧版副词条分母；新版忽略此字段，取合法词条池中最高五项系数之和。</summary>
    [JsonPropertyName("subStatMaximumBenefit")]
    public decimal SubStatMaximumBenefit { get; set; }
}
