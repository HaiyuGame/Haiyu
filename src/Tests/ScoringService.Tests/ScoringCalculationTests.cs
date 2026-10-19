using System.Text.Json;
using ScoringService;
using ScoringService.Models;
using ScoringService.Models.Enums;
using Calculator = ScoringService.ScoringService;

namespace ScoringService.Tests;

[TestClass]
public sealed class ScoringCalculationTests
{
    private readonly Calculator calculator = new();

    // 以下参数仅用于验证算术，不代表游戏实际档位或角色推荐值。
    private static EntryGroup Template() => new()
    {
        Id = "demo", Name = "基础计算样例", RoleId = "demo-role",
        MainStatMaximumBenefit = 1,
        SubStatMaximumBenefit = 1.5m,
        MainStatWeight = 0.3m, SubStatWeight = 0.7m,
        Items =
        [
            Rule("atk", EntryType.Percentage, EntryUsage.MainStatScoring, 30, 1, 0),
            Rule("atk", EntryType.Percentage, EntryUsage.SubStatScoring, 10, 1, 0),
            Rule("atk", EntryType.Value, EntryUsage.SubStatScoring, 40, 0.5m, 0),
            Rule("def", EntryType.Percentage, EntryUsage.SubStatScoring, 10, 0, 0.3m),
            new()
            {
                EntryNo = "recharge", EntryName = "共鸣效率", Type = EntryType.Percentage,
                Usage = EntryUsage.DevelopmentTarget, TargetValue = 260,
                Comparison = ComparisonOperator.GreaterThan
            }
        ]
    };

    private static EntryItem Rule(string id, EntryType type, EntryUsage usage,
        decimal reference, decimal weight, decimal penalty) => new()
    {
        EntryNo = id, EntryName = id, Type = type, Usage = usage,
        ReferenceValue = reference, Weight = weight, PenaltyCoefficient = penalty
    };

    private static StatValue Stat(string id, decimal value, EntryType type = EntryType.Percentage)
        => new() { EntryNo = id, Value = value, Type = type };

    private static EchoData Echo(decimal defense = 6) => new()
    {
        Id = "demo-echo",
        MainStats = [Stat("atk", 30)],
        SubStats = [Stat("atk", 8), Stat("atk", 24, EntryType.Value), Stat("def", defense)]
    };

    [TestMethod]
    public void BasicExampleMatchesHandCalculation()
    {
        var result = calculator.ScoreEcho(Template(), Echo());
        Assert.AreEqual(100m, result.MainStats.Score);
        Assert.AreEqual(1.1m, result.SubStats.Benefit); // 8/10*1 + 24/40*0.5
        Assert.AreEqual(0.18m, result.SubStats.Penalty); // 6/10*0.3
        Assert.AreEqual(61.333333333333333333333333330m, result.SubStats.Score, 0.00000001m);
        Assert.AreEqual(72.933333333333333333333333331m, result.TotalScore, 0.00000001m);
        Assert.AreEqual(-0.18m, result.SubStats.Details.Single(x => x.EntryNo == "def").NetContribution);
    }

    [TestMethod]
    public void PenaltyScalesWithValueInsteadOfOccurrence()
    {
        var low = calculator.ScoreEcho(Template(), Echo(3));
        var high = calculator.ScoreEcho(Template(), Echo(6));
        Assert.AreEqual(0.09m, low.SubStats.Penalty);
        Assert.AreEqual(low.SubStats.Penalty * 2, high.SubStats.Penalty);
        Assert.AreEqual(6m, low.SubStats.Score - high.SubStats.Score, 0.00000001m);
    }

    [TestMethod]
    public void PenaltyCoefficientScalesDeduction()
    {
        var template = Template();
        template.Items.Single(x => x.EntryNo == "def").PenaltyCoefficient = 0.6m;
        Assert.AreEqual(0.36m, calculator.ScoreEcho(template, Echo()).SubStats.Penalty);
    }

    [TestMethod]
    public void FixedAndPercentageAttributesRemainIndependent()
    {
        var result = calculator.ScoreEcho(Template(), Echo());
        Assert.AreEqual(0.8m, result.SubStats.Details.Single(x => x.EntryNo == "atk" &&
            x.Type == EntryType.Percentage).Benefit);
        Assert.AreEqual(0.3m, result.SubStats.Details.Single(x => x.Type == EntryType.Value).Benefit);
    }

    [TestMethod]
    public void MainAndSubRulesAreSeparate()
    {
        var echo = new EchoData { MainStats = [Stat("atk", 10)], SubStats = [Stat("atk", 10)] };
        var result = calculator.ScoreEcho(Template(), echo);
        Assert.AreEqual(1m / 3, result.MainStats.Details.Single().NormalizedValue);
        Assert.AreEqual(1m, result.SubStats.Details.Single().NormalizedValue);
    }

    [TestMethod]
    public void OneUsefulStatDoesNotBecomePerfectScore()
    {
        var result = calculator.ScoreEcho(Template(), new() { SubStats = [Stat("atk", 10)] });
        Assert.AreEqual(1.5m, result.SubStats.MaximumBenefit);
        Assert.AreEqual(100m / 1.5m, result.SubStats.Score, 0.00000001m);
    }

    [TestMethod]
    public void PerfectCombinationIsOneHundred()
    {
        var echo = new EchoData
        {
            MainStats = [Stat("atk", 30)],
            SubStats = [Stat("atk", 10), Stat("atk", 40, EntryType.Value)]
        };
        var result = calculator.ScoreEcho(Template(), echo);
        Assert.AreEqual(100m, result.MainStats.Score);
        Assert.AreEqual(100m, result.SubStats.Score);
        Assert.AreEqual(100m, result.TotalScore);
    }

    [TestMethod]
    public void EmptyEchoIsZero()
    {
        var result = calculator.ScoreEcho(Template(), new());
        Assert.AreEqual(0m, result.TotalScore);
        Assert.AreEqual(0, result.MainStats.Details.Count);
    }

    [TestMethod]
    public void ScoresAreClampedButContributionsRemainVisible()
    {
        var echo = new EchoData { MainStats = [Stat("atk", 60)], SubStats = [Stat("def", 10)] };
        var result = calculator.ScoreEcho(Template(), echo);
        Assert.AreEqual(100m, result.MainStats.Score);
        Assert.AreEqual(2m, result.MainStats.Benefit);
        Assert.AreEqual(0m, result.SubStats.Score);
        Assert.AreEqual(0.3m, result.SubStats.Penalty);
        Assert.AreEqual(30m, result.TotalScore);
    }

    [TestMethod]
    public void UnknownAttributesAreVisibleWithZeroContribution()
    {
        var result = calculator.ScoreEcho(Template(), new() { SubStats = [Stat("unknown", 10)] });
        var detail = result.SubStats.Details.Single();
        Assert.IsFalse(detail.IsMatched);
        Assert.AreEqual(0m, detail.NetContribution);
    }

    [TestMethod]
    public void ZeroCoefficientsIgnoreConfiguredAttribute()
    {
        var template = Template();
        template.Items.Single(x => x.EntryNo == "def").PenaltyCoefficient = 0;
        var result = calculator.ScoreEcho(template, Echo());
        Assert.AreEqual(0m, result.SubStats.Penalty);
        Assert.IsTrue(result.SubStats.Details.Single(x => x.EntryNo == "def").IsMatched);
    }

    [TestMethod]
    public void TemplateCanChangeMainAndSubWeights()
    {
        var template = Template();
        template.MainStatWeight = 1;
        template.SubStatWeight = 0;
        Assert.AreEqual(100m, calculator.ScoreEcho(template, Echo()).TotalScore);
    }

    [DataTestMethod]
    [DataRow(259, false)]
    [DataRow(260, false)]
    [DataRow(261, true)]
    public void StrictRechargeTargetChecksBoundary(int recharge, bool expected)
    {
        var panel = new CharacterPanel { Stats = [Stat("recharge", recharge)] };
        var result = calculator.EvaluateDevelopment(Template(), panel);
        Assert.AreEqual(expected, result.IsComplete);
        Assert.AreEqual(expected ? 1 : 0, result.MetCount);
        Assert.AreEqual((decimal)recharge, result.Targets.Single().ActualValue);
    }

    [DataTestMethod]
    [DataRow(ComparisonOperator.GreaterThan, false)]
    [DataRow(ComparisonOperator.GreaterThanOrEqual, true)]
    [DataRow(ComparisonOperator.LessThan, false)]
    [DataRow(ComparisonOperator.LessThanOrEqual, true)]
    [DataRow(ComparisonOperator.Equal, true)]
    public void ComparisonOperatorsAtEquality(ComparisonOperator comparison, bool expected)
    {
        var template = Template();
        template.Items.Single(x => x.Usage == EntryUsage.DevelopmentTarget).Comparison = comparison;
        var result = calculator.EvaluateDevelopment(template, new() { Stats = [Stat("recharge", 260)] });
        Assert.AreEqual(expected, result.IsComplete);
    }

    [TestMethod]
    public void StrictRechargeTargetPreservesFractionalPrecision()
    {
        var result = calculator.EvaluateDevelopment(Template(), new() { Stats = [Stat("recharge", 260.01m)] });
        Assert.IsTrue(result.IsComplete);
    }

    [TestMethod]
    public void MissingPanelAttributeIsNotZeroOrComplete()
    {
        var template = Template();
        var target = template.Items.Single(x => x.Usage == EntryUsage.DevelopmentTarget);
        target.TargetValue = 0;
        target.Comparison = ComparisonOperator.GreaterThanOrEqual;
        var result = calculator.EvaluateDevelopment(template, new());
        Assert.IsNull(result.Targets.Single().ActualValue);
        Assert.IsFalse(result.IsComplete);
    }

    [TestMethod]
    public void AllDevelopmentTargetsMustPass()
    {
        var template = Template();
        template.Items.Add(new()
        {
            EntryNo = "atk", Type = EntryType.Value, Usage = EntryUsage.DevelopmentTarget,
            TargetValue = 2000, Comparison = ComparisonOperator.GreaterThanOrEqual
        });
        var result = calculator.EvaluateDevelopment(template,
            new() { Stats = [Stat("recharge", 261), Stat("atk", 1900, EntryType.Value)] });
        Assert.AreEqual(1, result.MetCount);
        Assert.IsFalse(result.IsComplete);
    }

    [TestMethod]
    public void NoTargetsMeansUnconfiguredNotComplete()
    {
        var template = Template();
        template.Items.RemoveAll(x => x.Usage == EntryUsage.DevelopmentTarget);
        var result = calculator.EvaluateDevelopment(template, new());
        Assert.IsFalse(result.IsConfigured);
        Assert.IsFalse(result.IsComplete);
    }

    [TestMethod]
    public void PanelAndEquipmentScoresAreIndependent()
    {
        var template = Template();
        // 同一属性按不同用途配置，共鸣效率装备值不等于最终面板值。
        template.Items.Add(Rule("recharge", EntryType.Percentage, EntryUsage.SubStatScoring, 10, 0.5m, 0));
        template.SubStatMaximumBenefit = 2;
        var echo = new EchoData { SubStats = [Stat("recharge", 10)] };
        var low = calculator.Evaluate(template, echo, new() { Stats = [Stat("recharge", 259)] });
        var high = calculator.Evaluate(template, echo, new() { Stats = [Stat("recharge", 261)] });
        Assert.AreEqual(25m, low.EchoScore.SubStats.Score);
        Assert.AreEqual(low.EchoScore.TotalScore, high.EchoScore.TotalScore);
        Assert.IsFalse(low.Development.IsComplete);
        Assert.IsTrue(high.Development.IsComplete);
    }

    [TestMethod]
    public void InvalidRulesAreRejected()
    {
        var mutations = new Action<EntryGroup>[]
        {
            t => t.MainStatWeight = 0.4m,
            t => { t.MainStatWeight = -0.1m; t.SubStatWeight = 1.1m; },
            t => t.SubStatMaximumBenefit = 0,
            t => t.MainStatMaximumBenefit = -1,
            t => t.Items[0].ReferenceValue = 0,
            t => t.Items[0].ReferenceValue = null,
            t => t.Items[0].Weight = -1,
            t => t.Items[0].PenaltyCoefficient = -1,
            t => t.Items[0].PenaltyCoefficient = 0.1m,
            t => t.Items.Add(t.Items[0]),
            t => t.Items[0].EntryNo = " ",
            t => t.Items[0].Type = (EntryType)99,
            t => t.Items[0].Usage = (EntryUsage)99,
            t => t.Items[0].TargetValue = 1,
            t => t.Items[^1].TargetValue = null,
            t => t.Items[^1].TargetValue = -1,
            t => t.Items[^1].Comparison = null,
            t => t.Items[^1].Comparison = (ComparisonOperator)99,
            t => t.Items[^1].Weight = 1
        };
        foreach (var mutate in mutations)
        {
            var template = Template();
            mutate(template);
            Assert.ThrowsException<ArgumentException>(() => Calculator.ValidateTemplate(template));
        }
    }

    [TestMethod]
    public void InvalidOrDuplicateActualStatsAreRejected()
    {
        var invalidLists = new List<StatValue>[]
        {
            [Stat("atk", -1)], [Stat(" ", 1)], [Stat("atk", 1, (EntryType)99)],
            [Stat("atk", 1), Stat("atk", 2)]
        };
        foreach (var stats in invalidLists)
        {
            Assert.ThrowsException<ArgumentException>(() => calculator.ScoreEcho(Template(), new() { SubStats = stats }));
            Assert.ThrowsException<ArgumentException>(() => calculator.EvaluateDevelopment(Template(), new() { Stats = stats }));
        }
    }

    [TestMethod]
    public void JsonSourceGenerationRoundTripPreservesAllUsages()
    {
        var json = JsonSerializer.Serialize(Template(), ScoringJsonContext.Default.EntryGroup);
        var restored = JsonSerializer.Deserialize(json, ScoringJsonContext.Default.EntryGroup)!;
        Assert.AreEqual("demo-role", restored.RoleId);
        Assert.AreEqual(5, restored.Items.Count);
        Assert.AreEqual(0.3m, restored.Items[3].PenaltyCoefficient);
        Assert.AreEqual(ComparisonOperator.GreaterThan, restored.Items[4].Comparison);
        Assert.AreEqual(calculator.ScoreEcho(Template(), Echo()).TotalScore,
            calculator.ScoreEcho(restored, Echo()).TotalScore);
        Assert.IsTrue(json.Contains("DevelopmentTarget", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ShippedExampleTemplateMatchesDocumentedResult()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "basic-template.json"));
        var template = JsonSerializer.Deserialize(json, ScoringJsonContext.Default.EntryGroup)!;
        var result = calculator.Evaluate(template, Echo(),
            new() { Stats = [Stat("recharge", 260.01m)] });
        Assert.AreEqual(72.93333333m, result.EchoScore.TotalScore, 0.00000001m);
        Assert.IsTrue(result.Development.IsComplete);
    }

    [TestMethod]
    public void EvaluationDoesNotModifyInputs()
    {
        var template = Template();
        var before = JsonSerializer.Serialize(template, ScoringJsonContext.Default.EntryGroup);
        var echo = Echo();
        var panel = new CharacterPanel { Stats = [Stat("recharge", 261)] };
        calculator.Evaluate(template, echo, panel);
        Assert.AreEqual(before, JsonSerializer.Serialize(template, ScoringJsonContext.Default.EntryGroup));
        Assert.AreEqual(3, echo.SubStats.Count);
        Assert.AreEqual(6m, echo.SubStats[2].Value);
        Assert.AreEqual(261m, panel.Stats[0].Value);
    }

    [TestMethod]
    public void WrongPanelUnitDoesNotMatchTarget()
    {
        var result = calculator.EvaluateDevelopment(Template(),
            new() { Stats = [Stat("recharge", 300, EntryType.Value)] });
        Assert.IsNull(result.Targets.Single().ActualValue);
        Assert.IsFalse(result.IsComplete);
    }
}
