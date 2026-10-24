using System.Text.Json.Serialization;
using ScoringService.Models.Enums;

namespace ScoringService.Models;

/// <summary>实际属性。与规则按 (EntryNo, Type) 匹配，10% 表示为 10。</summary>
public sealed class StatValue
{
    /// <summary>实际属性编号，与模板规则的属性编号对应。</summary>
    [JsonPropertyName("entryNo")]
    public string EntryNo { get; set; } = "";

    /// <summary>实际属性的数值类型，固定值与百分比分别匹配。</summary>
    [JsonPropertyName("type")]
    public EntryType Type { get; set; }

    /// <summary>实际非负数值，10% 存为 10，固定攻击力 24 存为 24。</summary>
    [JsonPropertyName("value")]
    public decimal Value { get; set; }
}

/// <summary>单件声骸输入；一套声骸应逐件评分，不将重复属性混进单件列表。</summary>
public sealed class EchoData
{
    /// <summary>声骸 COST，当前算法支持 1、3、4。</summary>
    [JsonPropertyName("cost")]
    public int Cost { get; set; }

    /// <summary>声骸星级；当前主词条标准为五星满级。</summary>
    [JsonPropertyName("rarity")]
    public int Rarity { get; set; } = 5;

    /// <summary>强化等级，0～25；主词条按满级潜力标准评分，不将未强化视作满级。</summary>
    [JsonPropertyName("level")]
    public int Level { get; set; } = 25;
    /// <summary>单件声骸的标识，用于关联评分结果。</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>单件声骸的主词条，同一属性编号及类型组合唯一。</summary>
    [JsonPropertyName("mainStats")]
    public List<StatValue> MainStats { get; set; } = [];

    /// <summary>单件声骸的副词条，同一属性编号及类型组合唯一。</summary>
    [JsonPropertyName("subStats")]
    public List<StatValue> SubStats { get; set; } = [];
}

/// <summary>最终角色面板。由调用方统一武器、等级、战斗增益等口径。</summary>
public sealed class CharacterPanel
{
    /// <summary>统一口径的最终角色面板属性，缺失属性不按零值补齐。</summary>
    [JsonPropertyName("stats")]
    public List<StatValue> Stats { get; set; } = [];
}
