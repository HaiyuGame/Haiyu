using ScoringService.Models;
using ScoringService.Models.Enums;

namespace ScoringService;

/// <summary>纯计算核心，不依赖库街区、界面或文件初始化；结果保留精度，由界面格式化。</summary>
public sealed class ScoringService
{
    /// <summary>词条存储服务入口，调用初始化方法后赋值；不参与模型序列化。</summary>
    public ScoringEntryService? EntryService { get; private set; }

    public Task InitAsync(string baseFolder)
    {
        EntryService = new(baseFolder);
        return Task.CompletedTask;
    }

    public EchoScoreResult ScoreEcho(EntryGroup template, EchoData echo)
    {
        ValidateTemplate(template);
        ArgumentNullException.ThrowIfNull(echo);
        var main = ScoreStats(template, echo.MainStats, EntryUsage.MainStatScoring,
            template.MainStatMaximumBenefit);
        var sub = ScoreStats(template, echo.SubStats, EntryUsage.SubStatScoring,
            template.SubStatMaximumBenefit);
        return new(echo.Id, main, sub,
            main.Score * template.MainStatWeight + sub.Score * template.SubStatWeight);
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
        if (template.MainStatWeight < 0 || template.SubStatWeight < 0 ||
            template.MainStatWeight + template.SubStatWeight != 1)
            throw new ArgumentException("主、副词条占比必须非负，且合计为 1。", nameof(template));
        if (template.MainStatMaximumBenefit <= 0 || template.SubStatMaximumBenefit <= 0)
            throw new ArgumentException("主、副词条理论最大收益必须大于零。", nameof(template));

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
                if (rule.ReferenceValue.HasValue || rule.Weight.HasValue || rule.PenaltyCoefficient.HasValue)
                    throw new ArgumentException("养成规则应只配置目标参数。", nameof(template));
            }
            else
            {
                if (rule.ReferenceValue is null or <= 0 || rule.Weight is < 0 || rule.PenaltyCoefficient is < 0)
                    throw new ArgumentException("评分基准必须大于零，收益权重和惩罚系数应非负。", nameof(template));
                if (rule.Weight.GetValueOrDefault() > 0 && rule.PenaltyCoefficient.GetValueOrDefault() > 0)
                    throw new ArgumentException("同一规则的收益权重与惩罚系数互斥。", nameof(template));
                if (rule.TargetValue.HasValue || rule.Comparison.HasValue)
                    throw new ArgumentException("评分规则应只配置评分参数。", nameof(template));
            }
        }
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
