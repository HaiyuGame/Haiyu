using System.Text.Json.Serialization;

namespace ScoringService.Models;

/// <summary>角色模板。理论最大收益由模板作者按合法最优组合配置，与实际装备无关。</summary>
public sealed class EntryGroup
{
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

    /// <summary>主词条合法最优组合的理论最大收益，大于零，用作百分制分母。</summary>
    [JsonPropertyName("mainStatMaximumBenefit")]
    public decimal MainStatMaximumBenefit { get; set; }

    /// <summary>副词条合法最优组合的理论最大收益，大于零，不随实际有效词条数量变化。</summary>
    [JsonPropertyName("subStatMaximumBenefit")]
    public decimal SubStatMaximumBenefit { get; set; }
}
