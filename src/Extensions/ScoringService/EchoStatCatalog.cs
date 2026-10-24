using ScoringService.Models;
using ScoringService.Models.Enums;

namespace ScoringService;

/// <summary>内置属性定义。百分数采用百分点，例如 10.5% 存为 10.5。主词条为五星 +25 标准。</summary>
public sealed record EchoStatDefinition(string Id, string Name, EntryType Type, decimal SubMaximum)
{
    /// <summary>用于配置选择器的简短名称。</summary>
    public string Label => Name + (Type == EntryType.Percentage ? " %" : " · 固定");
    /// <summary>是否可用于对应评分用途。</summary>
    public bool Supports(EntryUsage usage) => usage == EntryUsage.DevelopmentTarget ||
        (usage == EntryUsage.SubStatScoring ? SubMaximum > 0 : new[] { 1, 3, 4 }.Any(cost => EchoStatCatalog.MainMaximum(Id, Type, cost) > 0));
}

/// <summary>所有常规声骸主副属性；数据来源与适用范围随代码版本固定，未知属性显式提示而非静默计零。</summary>
public static class EchoStatCatalog
{
    public const string SourceUrl = "https://www.prydwen.gg/wuthering-waves/guides/echo-stats/";
    public const string Revision = "2026-10-19 / 五星+25";
    private static readonly IReadOnlyList<EchoStatDefinition> definitions = Array.AsReadOnly<EchoStatDefinition>([
        new("atk", "攻击力", EntryType.Value, 60), new("atk", "攻击力", EntryType.Percentage, 11.6m),
        new("hp", "生命值", EntryType.Value, 580), new("hp", "生命值", EntryType.Percentage, 11.6m),
        new("def", "防御力", EntryType.Value, 70), new("def", "防御力", EntryType.Percentage, 14.7m),
        new("critRate", "暴击率", EntryType.Percentage, 10.5m), new("critDamage", "暴击伤害", EntryType.Percentage, 21),
        new("energyRegen", "共鸣效率", EntryType.Percentage, 12.4m),
        new("basicDamage", "普攻伤害加成", EntryType.Percentage, 11.6m),
        new("heavyDamage", "重击伤害加成", EntryType.Percentage, 11.6m),
        new("skillDamage", "共鸣技能伤害加成", EntryType.Percentage, 11.6m),
        new("liberationDamage", "共鸣解放伤害加成", EntryType.Percentage, 11.6m),
        new("glacioDamage", "冷凝伤害加成", EntryType.Percentage, 0),
        new("fusionDamage", "热熔伤害加成", EntryType.Percentage, 0),
        new("electroDamage", "导电伤害加成", EntryType.Percentage, 0),
        new("aeroDamage", "气动伤害加成", EntryType.Percentage, 0),
        new("spectroDamage", "衍射伤害加成", EntryType.Percentage, 0),
        new("havocDamage", "湮灭伤害加成", EntryType.Percentage, 0),
        new("healingBonus", "治疗效果加成", EntryType.Percentage, 0)
    ]);

    /// <summary>20 种属性（同名固定值与百分比独立），其中 13 种可作为副词条。</summary>
    public static IReadOnlyList<EchoStatDefinition> All => definitions;
    public static EchoStatDefinition? Find(string id, EntryType type) => All.FirstOrDefault(x => x.Id == id && x.Type == type);

    /// <summary>按 COST 返回主词条满级上限；零表示该 COST 无此主属性。</summary>
    public static decimal MainMaximum(string id, EntryType type, int cost)
    {
        if (cost is not (1 or 3 or 4)) return 0;
        if (type == EntryType.Value)
            return (cost, id) switch { (1, "hp") => 2280, (3, "atk") => 100, (4, "atk") => 150, _ => 0 };
        return id switch
        {
            "atk" => cost switch { 1 => 18, 3 => 30, _ => 33 },
            "hp" => cost switch { 1 => 22.8m, 3 => 30, _ => 33 },
            "def" => cost switch { 1 => 18, 3 => 38, _ => 41.8m },
            "energyRegen" when cost == 3 => 32,
            "critRate" when cost == 4 => 22,
            "critDamage" when cost == 4 => 44,
            "healingBonus" when cost == 4 => 26.4m,
            "glacioDamage" or "fusionDamage" or "electroDamage" or "aeroDamage" or "spectroDamage" or "havocDamage" when cost == 3 => 30,
            _ => 0
        };
    }

    /// <summary>补齐旧模板遗漏的合法主副词条；已有系数和养成目标保持不变，新词条默认零收益。</summary>
    public static void CompleteRules(EntryGroup template)
    {
        foreach (var usage in new[] { EntryUsage.MainStatScoring, EntryUsage.SubStatScoring })
            foreach (var stat in All.Where(x => x.Supports(usage)))
                if (!template.Items.Any(x => x.Usage == usage && x.EntryNo == stat.Id && x.Type == stat.Type))
                    template.Items.Add(Rule(stat, usage));
    }

    /// <summary>创建独立可编辑规则；不会改写内置定义。</summary>
    public static EntryItem Rule(EchoStatDefinition stat, EntryUsage usage, decimal weight = 0) => new()
    {
        EntryNo = stat.Id, EntryName = stat.Name, Type = stat.Type, Usage = usage,
        ReferenceValue = usage == EntryUsage.DevelopmentTarget ? null : stat.SubMaximum > 0 ? stat.SubMaximum : 1,
        Weight = usage == EntryUsage.DevelopmentTarget ? null : weight,
        TargetValue = usage == EntryUsage.DevelopmentTarget ? 0 : null,
        Comparison = usage == EntryUsage.DevelopmentTarget ? ComparisonOperator.GreaterThanOrEqual : null
    };
}
