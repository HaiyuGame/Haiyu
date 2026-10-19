using ScoringService.Models;
using ScoringService.Models.Enums;

namespace ScoringService;

/// <summary>按公开培养攻略的优先级制作的经验模板。系数由本项目定义，不冒充网站原始评分。</summary>
public static class CharacterScorePresets
{
    public static EntryGroup Jinhsi() => Create("1304", "今汐", "jinhsi", "spectroDamage", "skillDamage", 125);
    public static EntryGroup Camellya() => Create("1603", "椿", "camellya", "havocDamage", "basicDamage", 120);
    public static EntryGroup XiangliYao() => Create("1305", "相里要", "xiangli-yao", "electroDamage", "liberationDamage", 125);

    public static EntryGroup Cartethyia()
    {
        var template = Create("1409", "卡提希娅", "cartethyia", "aeroDamage", "basicDamage", 110);
        foreach (var item in template.Items.Where(x => x.Usage != EntryUsage.DevelopmentTarget))
            item.Weight = item.Usage == EntryUsage.MainStatScoring
                ? item.EntryNo switch { "critRate" or "critDamage" => 1m, "hp" when item.Type == EntryType.Percentage => 1m, "aeroDamage" => 0.9m, _ => 0m }
                : item.EntryNo switch
                {
                    "critRate" or "critDamage" => 1m, "hp" when item.Type == EntryType.Percentage => 0.8m,
                    "hp" => 0.3m, "basicDamage" or "liberationDamage" => 0.6m, "energyRegen" => 0.5m, _ => 0m
                };
        template.Notes = "2026-10-19。生命倍率输出；44111 双暴/双暴/生命/生命/生命参考。系数为本工具经验值，百分比按理论最优组合归一化，不与其他工具的毕业分直接等同。";
        return template;
    }

    public static EntryGroup Shorekeeper()
    {
        var template = Create("1505", "守岸人", "the-shorekeeper", "spectroDamage", "liberationDamage", 250);
        foreach (var item in template.Items.Where(x => x.Usage != EntryUsage.DevelopmentTarget))
            item.Weight = item.EntryNo switch
            {
                "energyRegen" => 1, "hp" when item.Type == EntryType.Percentage => 0.8m,
                "hp" => 0.3m, "healingBonus" => 1,
                "critDamage" => 0.4m, _ => 0
            };
        template.Notes = "参考守岸人培养优先级；250%为参考目标，武器与队伍可能改变需求。数字系数为本工具经验值。";
        return template;
    }

    /// <summary>未知角色不套用其他角色的正式推荐；提供明确命名的可编辑起点。</summary>
    public static EntryGroup Generic(string roleId, string name) => Create(roleId, name + " · 自定义", "", "", "", 0);
    public static IReadOnlyList<EntryGroup> All => [Jinhsi(), Camellya(), XiangliYao(), Shorekeeper(), Cartethyia()];
    public static bool IsUnconfiguredStart(EntryGroup template) => template.Id == "builtin-" + template.RoleId && string.IsNullOrWhiteSpace(template.SourceUrl);

    public static EntryGroup ForRole(string roleId, string name) => All.FirstOrDefault(x => x.RoleId == roleId) ?? Generic(roleId, name);

    private static EntryGroup Create(string roleId, string name, string slug, string element, string damage, decimal energyTarget)
    {
        var template = new EntryGroup
        {
            Id = "builtin-" + roleId, RoleId = roleId, Name = name,
            SourceUrl = slug.Length > 0 ? "https://www.prydwen.gg/wuthering-waves/characters/" + slug + "/" : "",
            Notes = "2026-10-19。攻略优先级参考；折算系数是本工具经验值，结果为静态适配分，不是伤害提升率。",
            MainStatWeight = 0.3m, SubStatWeight = 0.7m
        };
        foreach (var usage in new[] { EntryUsage.MainStatScoring, EntryUsage.SubStatScoring })
            foreach (var stat in EchoStatCatalog.All.Where(x => x.Supports(usage)))
            {
                var weight = stat.Id switch
                {
                    "critRate" or "critDamage" => 1m,
                    "atk" when stat.Type == EntryType.Percentage => usage == EntryUsage.MainStatScoring ? 0.9m : 0.8m,
                    "atk" when usage == EntryUsage.SubStatScoring => 0.3m,
                    "energyRegen" => 0.5m,
                    _ when stat.Id == element && element.Length > 0 => 1m,
                    _ when stat.Id == damage && damage.Length > 0 => 0.8m,
                    _ => 0m
                };
                template.Items.Add(EchoStatCatalog.Rule(stat, usage, weight));
            }
        if (energyTarget > 0)
        {
            var target = EchoStatCatalog.Rule(EchoStatCatalog.Find("energyRegen", EntryType.Percentage)!, EntryUsage.DevelopmentTarget);
            target.TargetValue = energyTarget;
            template.Items.Add(target);
        }
        return template;
    }
}
