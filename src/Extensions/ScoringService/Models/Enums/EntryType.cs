namespace ScoringService.Models.Enums;

/// <summary>属性数值的单位类型，不影响评分权重的存储方式。</summary>
public enum EntryType:uint
{
    /// <summary>
    /// 百分比，10% 存为 10。
    /// </summary>
    Percentage,
    /// <summary>
    /// 固定数值，例如固定攻击力 24 存为 24。
    /// </summary>
    Value
}
