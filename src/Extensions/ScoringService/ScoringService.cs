using ScoringService.Models;
using ScoringService.Models.Enums;

namespace ScoringService;

/// <summary>纯计算核心，不依赖库街区、界面或文件初始化；结果保留精度，由界面格式化。</summary>
public sealed class ScoringService
{
    /// <summary>词条存储服务入口，调用初始化方法后赋值；不参与模型序列化。</summary>
    public ScoringEntryService EntryService { get; private set; }

    public Task InitAsync(SortingOption option)
    {
        EntryService = new(option);
        return Task.CompletedTask;
    }

    public EchoScoreResult ScoreEcho(EntryGroup template, EchoData echo)
    {
        ValidateTemplate(template);
        ArgumentNullException.ThrowIfNull(echo);
        if (template.ScoringVersion == 2)
            return ScoreCurrent(template, echo);
        var main = ScoreStats(template, echo.MainStats, EntryUsage.MainStatScoring,
            template.MainStatMaximumBenefit);
        var sub = ScoreStats(template, echo.SubStats, EntryUsage.SubStatScoring,
            template.SubStatMaximumBenefit);
        return new(echo.Id, main, sub,
            main.Score * template.MainStatWeight + sub.Score * template.SubStatWeight);
    }

    /// <summary>以角色毕业基准评分；原ScoreEcho保留五项理论口径，避免旧调用方悄悄变分。</summary>
    public GraduationScoreResult ScoreGraduation(EntryGroup template, EchoData echo)
    {
        ValidateTemplate(template);
        ArgumentNullException.ThrowIfNull(echo);
        if (template.ScoringVersion != 2) throw new ArgumentException("毕业评分需要新版模板。");
        if (template.SubStatWeight > 0 && template.Items.Count(x => x.Usage == EntryUsage.SubStatScoring && x.Weight > 0) < template.TargetEffectiveStatCount)
            throw new ArgumentException("正收益副词条种类少于目标有效词条数，请补充系数或降低目标数量。");
        var raw = ScoreCurrent(template, echo, template.TargetEffectiveStatCount);
        var main = raw.MainStats with { Score = Math.Min(raw.MainStats.Score, 100) };
        var sub = raw.SubStats with { Score = Math.Min(raw.SubStats.Score, 100) };
        var score = raw with { MainStats = main, SubStats = sub, TotalScore = main.Score * template.MainStatWeight + sub.Score * template.SubStatWeight };
        return new(score, raw.TotalScore, ScoreCurrent(template, echo, 5, false).TotalScore);
    }

    public DevelopmentResult EvaluateDevelopment(EntryGroup template, CharacterPanel panel)


    {
        ValidateTemplate(template);
        ArgumentNullException.ThrowIfNull(panel);
        var actual = IndexStats(panel.Stats);
        var results = template.Items
            .Where(x => x.Usage == EntryUsage.DevelopmentTarget)
            .Select(rule =>
            {
                decimal? value = actual.TryGetValue((rule.EntryNo, rule.Type), out var stat)
                    ? stat.Value : null;
                var target = rule.TargetValue!.Value;
                var comparison = rule.Comparison!.Value;
                return new DevelopmentTargetResult(rule.EntryNo, rule.EntryName, rule.Type,
                    value, target, comparison, value.HasValue && Compare(value.Value, target, comparison));
            }).ToArray();
        return new(results);
    }

    public CharacterEvaluationResult Evaluate(EntryGroup template, EchoData echo, CharacterPanel panel)
        => new(ScoreEcho(template, echo), EvaluateDevelopment(template, panel));

    public static void ValidateTemplate(EntryGroup template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.ScoringVersion is not (1 or 2))
            throw new ArgumentException("评分版本不受支持。", nameof(template));
        if (template.MainStatWeight < 0 || template.SubStatWeight < 0 ||
            template.MainStatWeight + template.SubStatWeight != 1)
            throw new ArgumentException("主、副词条占比必须非负，且合计为 1。", nameof(template));
        if (template.ScoringVersion == 1 && (template.MainStatMaximumBenefit <= 0 || template.SubStatMaximumBenefit <= 0))
            throw new ArgumentException("主、副词条理论最大收益必须大于零。", nameof(template));

        if (template.TargetEffectiveStatCount is < 1 or > 5)
            throw new ArgumentException("目标有效副词条数需为1～5。");
        ScoreRatings.Validate(template.GradeThresholds);
        ArgumentNullException.ThrowIfNull(template.Items);
        var keys = new HashSet<(EntryUsage, string, EntryType)>();
        foreach (var rule in template.Items)
        {
            ArgumentNullException.ThrowIfNull(rule);
            if (string.IsNullOrWhiteSpace(rule.EntryNo) || !Enum.IsDefined(rule.Type) ||
                !Enum.IsDefined(rule.Usage))
                throw new ArgumentException("规则需要有效的属性编号、类型和用途。", nameof(template));
            if (!keys.Add((rule.Usage, rule.EntryNo, rule.Type)))
                throw new ArgumentException("同一用途中的属性规则重复。", nameof(template));

            if (rule.Usage == EntryUsage.DevelopmentTarget)
            {
                if (rule.TargetValue is null or < 0 || rule.Comparison is null ||
                    !Enum.IsDefined(rule.Comparison.Value))
                    throw new ArgumentException("养成规则需要非负目标值和比较方式。", nameof(template));
                if (rule.ReferenceValue.HasValue || rule.Weight.HasValue || rule.PenaltyCoefficient.HasValue || rule.NormalizationReference.HasValue)
                    throw new ArgumentException("养成规则应只配置目标参数。", nameof(template));
            }
            else
            {
                if (template.ScoringVersion == 2)
                {
                    if (rule.PenaltyCoefficient.GetValueOrDefault() != 0)
                        throw new ArgumentException("旧惩罚参数需要迁移为折算系数；无效词条系数设为零。", nameof(template));
                    var definition = EchoStatCatalog.Find(rule.EntryNo, rule.Type);
                    if (definition is null || !definition.Supports(rule.Usage) || rule.Weight is null or < 0 or > 1)
                        throw new ArgumentException("请选择内置词条，并设置 0～1 的折算系数。", nameof(template));
                    if (rule.NormalizationReference is <= 0)
                        throw new ArgumentException("角色评分基准必须大于零；留空使用游戏满档。", nameof(template));
                    if (rule.TargetValue.HasValue || rule.Comparison.HasValue)
                        throw new ArgumentException("评分规则应只配置评分参数。", nameof(template));
                    continue;
                }
                if (rule.ReferenceValue is null or <= 0 || rule.Weight is < 0 || rule.PenaltyCoefficient is < 0)
                    throw new ArgumentException("评分基准必须大于零，收益权重和惩罚系数应非负。", nameof(template));
                if (rule.Weight.GetValueOrDefault() > 0 && rule.PenaltyCoefficient.GetValueOrDefault() > 0)
                    throw new ArgumentException("同一规则的收益权重与惩罚系数互斥。", nameof(template));
                if (rule.TargetValue.HasValue || rule.Comparison.HasValue)
                    throw new ArgumentException("评分规则应只配置评分参数。", nameof(template));
            }
        }
        if (template.ScoringVersion == 2)
        {
            if (template.SubStatWeight > 0 && !template.Items.Any(x => x.Usage == EntryUsage.SubStatScoring && x.Weight > 0))
                throw new ArgumentException("请至少设置一项副词条正系数。");
            if (template.MainStatWeight > 0)
                foreach (var cost in new[] { 1, 3, 4 })
                    if (!template.Items.Any(x => x.Usage == EntryUsage.MainStatScoring && x.Weight > 0 && EchoStatCatalog.MainMaximum(x.EntryNo, x.Type, cost) > 0))
                        throw new ArgumentException($"请为 COST {cost} 设置至少一项主词条正系数。");
        }
    }

    /// <summary>副词条采用完整合法词条池中最高五项系数作固定分母；主词条按 COST 比较一个可选词条与固定词条。</summary>
    private static EchoScoreResult ScoreCurrent(EntryGroup template, EchoData echo, int targetCount = 5, bool useRoleReferences = true)
    {
        if (echo.Rarity != 5 || echo.Cost is not (1 or 3 or 4) || echo.Level is < 0 or > 25)
            throw new ArgumentException("当前评分支持五星、COST 1/3/4、等级 0～25 的声骸。");
        var main = IndexStats(echo.MainStats);
        var sub = IndexStats(echo.SubStats);
        if (sub.Count > 5 || sub.Count > echo.Level / 5)
            throw new ArgumentException("副词条数量超过已解锁槽位。");
        var primary = main.Values.Where(x => EchoStatCatalog.MainMaximum(x.EntryNo, x.Type, echo.Cost) > 0 && x.Type == EntryType.Percentage).ToArray();
        if (primary.Length > 1 || main.Count > 2)
            throw new ArgumentException("主词条应由一个可选属性和一个固定属性组成。");
        foreach (var stat in main.Values)
            if (EchoStatCatalog.MainMaximum(stat.EntryNo, stat.Type, echo.Cost) <= 0)
                throw new ArgumentException($"COST {echo.Cost} 的主词条不支持 {stat.EntryNo}。");
        foreach (var stat in sub.Values)
            if (EchoStatCatalog.Find(stat.EntryNo, stat.Type)?.SubMaximum is not > 0)
                throw new ArgumentException($"副词条不支持 {stat.EntryNo}，请更新内置词条数据。");
        decimal Weight(string id, EntryType type, EntryUsage usage) => template.Items
            .FirstOrDefault(x => x.EntryNo == id && x.Type == type && x.Usage == usage)?.Weight ?? 0;
        var mainMaximum = EchoStatCatalog.All.Where(x => x.Type == EntryType.Percentage && EchoStatCatalog.MainMaximum(x.Id, x.Type, echo.Cost) > 0)
            .Select(x => Weight(x.Id, x.Type, EntryUsage.MainStatScoring)).DefaultIfEmpty().Max();
        var fixedId = echo.Cost == 1 ? "hp" : "atk";
        mainMaximum += Weight(fixedId, EntryType.Value, EntryUsage.MainStatScoring);
        var subMaximum = EchoStatCatalog.All.Where(x => x.SubMaximum > 0)
            .Select(x => Weight(x.Id, x.Type, EntryUsage.SubStatScoring)).OrderDescending().Take(targetCount).Sum();
        if ((mainMaximum == 0 && template.MainStatWeight > 0) || (subMaximum == 0 && template.SubStatWeight > 0))
            throw new ArgumentException("参与总分的主、副词条至少需要一项正系数。");
        StatScoreResult Section(IEnumerable<StatValue> stats, EntryUsage usage, decimal maximum)
        {
            var details = new List<EntryScoreDetail>();
            foreach (var stat in stats)
            {
                var limit = usage == EntryUsage.MainStatScoring
                    ? EchoStatCatalog.MainMaximum(stat.EntryNo, stat.Type, echo.Cost)
                    : EchoStatCatalog.Find(stat.EntryNo, stat.Type)!.SubMaximum;
                if (stat.Value > limit)
                    throw new ArgumentException($"属性 {stat.EntryNo} 超出内置上限 {limit}。");
                var rule = template.Items.FirstOrDefault(x => x.EntryNo == stat.EntryNo && x.Type == stat.Type && x.Usage == usage);
                // 游戏上限用于校验；角色模板基准用于评分，达到基准即满收益。
                var ratio = Math.Min(stat.Value / (useRoleReferences ? rule?.NormalizationReference ?? limit : limit), 1m);
                details.Add(new(stat.EntryNo, stat.Type, stat.Value, rule is not null, ratio, ratio * (rule?.Weight ?? 0), 0));
            }
            return new(maximum == 0 ? 0 : details.Sum(x => x.Benefit) / maximum * 100, maximum, details);
        }
        var mainResult = Section(main.Values, EntryUsage.MainStatScoring, mainMaximum);
        var subResult = Section(sub.Values, EntryUsage.SubStatScoring, subMaximum);
        return new(echo.Id, mainResult, subResult, mainResult.Score * template.MainStatWeight + subResult.Score * template.SubStatWeight);
    }

    private static StatScoreResult ScoreStats(EntryGroup template, List<StatValue> stats,
        EntryUsage usage, decimal maximumBenefit)
    {
        var actual = IndexStats(stats);
        var rules = template.Items.Where(x => x.Usage == usage)
            .ToDictionary(x => (x.EntryNo, x.Type));
        var details = new List<EntryScoreDetail>();
        foreach (var (key, stat) in actual)
        {
            if (!rules.TryGetValue(key, out var rule))
            {
                // 未配置属性贡献为零，但保留明细供界面提示或补充规则。
                details.Add(new(stat.EntryNo, stat.Type, stat.Value, false, 0, 0, 0));
                continue;
            }
            var normalized = stat.Value / rule.ReferenceValue!.Value;
            details.Add(new(stat.EntryNo, stat.Type, stat.Value, true, normalized,
                normalized * rule.Weight.GetValueOrDefault(),
                normalized * rule.PenaltyCoefficient.GetValueOrDefault()));
        }
        var net = details.Sum(x => x.NetContribution);
        return new(Math.Clamp(net / maximumBenefit * 100, 0, 100), maximumBenefit, details.AsReadOnly());
    }

    private static Dictionary<(string, EntryType), StatValue> IndexStats(List<StatValue> stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var result = new Dictionary<(string, EntryType), StatValue>();
        foreach (var stat in stats)
        {
            ArgumentNullException.ThrowIfNull(stat);
            if (string.IsNullOrWhiteSpace(stat.EntryNo) || !Enum.IsDefined(stat.Type) || stat.Value < 0)
                throw new ArgumentException("实际属性需要有效编号、类型和非负数值。", nameof(stats));
            if (!result.TryAdd((stat.EntryNo, stat.Type), stat))
                throw new ArgumentException("同一输入中的属性重复；请先汇总面板或逐件评分。", nameof(stats));
        }
        return result;
    }

    private static bool Compare(decimal actual, decimal target, ComparisonOperator comparison) => comparison switch
    {
        ComparisonOperator.GreaterThan => actual > target,
        ComparisonOperator.GreaterThanOrEqual => actual >= target,
        ComparisonOperator.LessThan => actual < target,
        ComparisonOperator.LessThanOrEqual => actual <= target,
        ComparisonOperator.Equal => actual == target,
        _ => throw new ArgumentOutOfRangeException(nameof(comparison))
    };
}
