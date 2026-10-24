using System.Text.Json;
using ScoringService.Models;
using ScoringService.Models.Enums;
namespace ScoringService.Tests;

[TestClass]
public sealed class GraduationScoringTests
{
    private readonly global::ScoringService.ScoringService calculator = new();
    private static EntryGroup Template()
    {
        var group = CharacterScorePresets.Generic("test", "测试");
        group.MainStatWeight = 0; group.SubStatWeight = 1;
        foreach (var item in group.Items.Where(x => x.Usage == EntryUsage.SubStatScoring)) item.Weight = 1;
        return group;
    }
    private static EchoData Echo(int count, decimal ratio = 1) => new()
    {
        Cost = 3, Level = 25, Rarity = 5,
        SubStats = EchoStatCatalog.All.Where(x => x.SubMaximum > 0).Take(count)
            .Select(x => new StatValue { EntryNo = x.Id, Type = x.Type, Value = x.SubMaximum * ratio }).ToList()
    };
    [TestMethod] public void FourEffectiveScreenshotUsesRoleGraduationBenchmarksRatherThanForcedGrade()
    {
        var group = CharacterScorePresets.XiangliYao();
        foreach (var rule in group.Items.Where(x => x.Usage == EntryUsage.SubStatScoring))
            rule.Weight = rule.EntryNo switch { "critRate" or "critDamage" => 1m, "atk" when rule.Type == EntryType.Percentage => 0.8m, "energyRegen" or "liberationDamage" => 0.5m, _ => 0m };
        group.Items.Single(x => x.Usage == EntryUsage.MainStatScoring && x.EntryNo == "atk" && x.Type == EntryType.Value).Weight = 0.5m;
        var echo = new EchoData { Cost = 3, Level = 25, MainStats =
            [new() { EntryNo = "electroDamage", Type = EntryType.Percentage, Value = 30 }, new() { EntryNo = "atk", Type = EntryType.Value, Value = 100 }],
            SubStats = [new() { EntryNo = "def", Type = EntryType.Value, Value = 50 },
                new() { EntryNo = "energyRegen", Type = EntryType.Percentage, Value = 8.4m },
                new() { EntryNo = "critDamage", Type = EntryType.Percentage, Value = 18.6m },
                new() { EntryNo = "liberationDamage", Type = EntryType.Percentage, Value = 6.4m },
                new() { EntryNo = "critRate", Type = EntryType.Percentage, Value = 8.7m }] };
        var old = calculator.ScoreEcho(group, echo);
        Assert.AreEqual(72.9m, decimal.Round(old.TotalScore, 1));
        Assert.IsTrue(calculator.ScoreGraduation(group, echo).Score.TotalScore > old.TotalScore);
        foreach (var rule in group.Items.Where(x => x.Usage == EntryUsage.SubStatScoring))
            rule.NormalizationReference = rule.EntryNo switch { "critRate" => 8m, "critDamage" => 16m, "energyRegen" => 8m, "liberationDamage" => 6m, _ => null };
        var configured = calculator.ScoreGraduation(group, echo);
        Assert.AreEqual(93.6m, decimal.Round(configured.Score.TotalScore, 1));
        Assert.AreEqual("SS", ScoreRatings.Grade(configured.Score.TotalScore, group.GradeThresholds));
        Assert.AreEqual(old.TotalScore, configured.TheoreticalScore);
    }

    [TestMethod] public void TooFewPositiveStatKindsCannotPretendToMeetFourStatGoal()
    {
        var group = Template();
        foreach (var rule in group.Items.Where(x => x.Usage == EntryUsage.SubStatScoring)) rule.Weight = rule.EntryNo == "critRate" ? 1 : 0;
        Assert.ThrowsException<ArgumentException>(() => calculator.ScoreGraduation(group, Echo(4)));
    }
    [TestMethod] public void FourFullEffectiveStatsMeetFourStatGraduationButNotFiveStatTheoreticalLimit()
    {
        var group = Template(); var echo = Echo(4);
        var result = calculator.ScoreGraduation(group, echo);
        Assert.AreEqual(100m, result.Score.TotalScore);
        Assert.AreEqual(80m, result.TheoreticalScore);
        Assert.AreEqual(80m, calculator.ScoreEcho(group, echo).TotalScore);
        group.TargetEffectiveStatCount = 5;
        Assert.AreEqual(80m, calculator.ScoreGraduation(group, echo).Score.TotalScore);
    }
    [TestMethod] public void FifthEffectiveStatAddsProgressWithoutExceedingDisplayCap()
    {
        var result = calculator.ScoreGraduation(Template(), Echo(5));
        Assert.AreEqual(125m, result.Progress);
        Assert.AreEqual(100m, result.Score.TotalScore);
        Assert.AreEqual(100m, result.TheoreticalScore);
    }
    [TestMethod] public void FourWeakStatsAreNotAutomaticallyGraduated()
    {
        var group = Template(); var result = calculator.ScoreGraduation(group, Echo(4, 0.5m));
        Assert.AreEqual(50m, result.Score.TotalScore);
        Assert.IsTrue(result.Score.TotalScore < group.GradeThresholds.Graduation);
    }
    [TestMethod] public void RoleReferenceDoesNotChangeTheoreticalQualityOrOtherRole()
    {
        var group = Template(); var echo = Echo(4, 0.5m);
        foreach (var rule in group.Items.Where(x => x.Usage == EntryUsage.SubStatScoring))
            rule.NormalizationReference = EchoStatCatalog.Find(rule.EntryNo, rule.Type)!.SubMaximum / 2;
        var result = calculator.ScoreGraduation(group, echo);
        Assert.AreEqual(100m, result.Score.TotalScore);
        Assert.AreEqual(40m, result.TheoreticalScore);
        Assert.AreEqual(50m, calculator.ScoreGraduation(Template(), echo).Score.TotalScore);
    }
    [TestMethod] public void ThresholdsAndCountRoundTripWithSourceGeneration()
    {
        var group = Template(); group.TargetEffectiveStatCount = 3;
        group.GradeThresholds.SSS = 90; group.GradeThresholds.Graduation = 80;
        var loaded = JsonSerializer.Deserialize(JsonSerializer.Serialize(group, ScoringJsonContext.Default.EntryGroup), ScoringJsonContext.Default.EntryGroup)!;
        Assert.AreEqual(3, loaded.TargetEffectiveStatCount);
        Assert.AreEqual("SSS", ScoreRatings.Grade(92, loaded.GradeThresholds));
        Assert.AreEqual("SS", ScoreRatings.Grade(92));
        loaded.GradeThresholds.S = 99;
        Assert.ThrowsException<ArgumentException>(() => calculator.ScoreGraduation(loaded, Echo(4)));
    }
    [TestMethod] public void PanelTargetsDoNotChangeEchoGraduationScore()
    {
        var group = Template(); var before = calculator.ScoreGraduation(group, Echo(4)).Score.TotalScore;
        var target = EchoStatCatalog.Rule(EchoStatCatalog.Find("energyRegen", EntryType.Percentage)!, EntryUsage.DevelopmentTarget);
        target.TargetValue = 260; group.Items.Add(target);
        Assert.AreEqual(before, calculator.ScoreGraduation(group, Echo(4)).Score.TotalScore);
        Assert.IsFalse(calculator.EvaluateDevelopment(group, new CharacterPanel()).Targets.Single().IsMet);
    }
}
