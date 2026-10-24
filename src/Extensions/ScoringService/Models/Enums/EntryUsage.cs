namespace ScoringService.Models.Enums;

/// <summary>词条规则的业务用途，区分装备评分与角色养成判定。</summary>
public enum EntryUsage
{
    /// <summary>用于单件声骸主词条评分。</summary>
    MainStatScoring,
    /// <summary>用于单件声骸副词条评分。</summary>
    SubStatScoring,
    /// <summary>用于检查角色最终面板是否达到养成目标。</summary>
    DevelopmentTarget
}
