using System.Text.Json;
using ScoringService.Models;
using ScoringService.Models.Enums;
using Calculator = ScoringService.ScoringService;

namespace ScoringService.Tests;

[TestClass]
public sealed class StaticScoringV2Tests
{
    private readonly Calculator calculator = new();
    private static StatValue Stat(string id, decimal value, EntryType type = EntryType.Percentage) => new() { EntryNo = id, Type = type, Value = value };
    private static EchoData Echo(params StatValue[] sub) => new()
    { Cost = 4, MainStats = [Stat("critRate", 22), Stat("atk", 150, EntryType.Value)], SubStats = sub.ToList() };
    private static EntryItem Rule(EntryGroup group, string id, EntryType type = EntryType.Percentage, EntryUsage usage = EntryUsage.SubStatScoring) =>
        group.Items.Single(x => x.EntryNo == id && x.Type == type && x.Usage == usage);

    [TestMethod]
    public void CharacterReferenceIsIndependentOfGameMaximumAndOtherTemplates()
    {
        var first = CharacterScorePresets.Camellya();
        var second = CharacterScorePresets.Camellya();
        Rule(first, "critRate").NormalizationReference = 8m;
        var echo = Echo(Stat("critRate", 8.7m));
        var adjusted = calculator.ScoreEcho(first, echo).SubStats.Details.Single();
        var original = calculator.ScoreEcho(second, echo).SubStats.Details.Single();
        Assert.AreEqual(1m, adjusted.NormalizedValue);
        Assert.AreEqual(8.7m / 10.5m, original.NormalizedValue);
        Assert.IsNull(Rule(second, "critRate").NormalizationReference);
        var json = JsonSerializer.Serialize(first, ScoringJsonContext.Default.EntryGroup);
        var saved = JsonSerializer.Deserialize(json, ScoringJsonContext.Default.EntryGroup)!;
        Assert.AreEqual(8m, Rule(saved, "critRate").NormalizationReference);
        Assert.ThrowsException<ArgumentException>(() => calculator.ScoreEcho(first, Echo(Stat("critRate", 50))));
    }

    [TestMethod]
    public void CharacterReferenceMustBePositive()
    {
        var group = CharacterScorePresets.Camellya();
        Rule(group, "critRate").NormalizationReference = 0;
        Assert.ThrowsException<ArgumentException>(() => calculator.ScoreEcho(group, Echo()));
    }

    [TestMethod]
    public void CompletingOldTemplatePreservesWeightsAndIncludesThreeCostMainStats()
    {
        var group = new EntryGroup();
        var attack = EchoStatCatalog.Rule(EchoStatCatalog.Find("atk", EntryType.Percentage)!, EntryUsage.MainStatScoring, 0.65m);
        group.Items.Add(attack);
        EchoStatCatalog.CompleteRules(group);
        EchoStatCatalog.CompleteRules(group);
        Assert.AreEqual(0.65m, Rule(group, "atk", usage: EntryUsage.MainStatScoring).Weight);
        Assert.AreEqual(0m, Rule(group, "electroDamage", usage: EntryUsage.MainStatScoring).Weight);
        Assert.AreEqual(30m, EchoStatCatalog.MainMaximum(attack.EntryNo, attack.Type, 3));
        Assert.AreEqual(13, group.Items.Count(x => x.Usage == EntryUsage.SubStatScoring));
        Assert.AreEqual(EchoStatCatalog.All.Count(x => x.Supports(EntryUsage.MainStatScoring)), group.Items.Count(x => x.Usage == EntryUsage.MainStatScoring));
    }

    [TestMethod]
    public void UnversionedJsonIsLegacyWhileNewTemplatesDefaultToVersionTwo()
    {
        Assert.AreEqual(2, new EntryGroup().ScoringVersion);
        Assert.AreEqual(1, JsonSerializer.Deserialize("{}", ScoringJsonContext.Default.EntryGroup)!.ScoringVersion);
        Assert.AreEqual(2, JsonSerializer.Deserialize("{\"scoringVersion\":2}", ScoringJsonContext.Default.EntryGroup)!.ScoringVersion);
    }

    [TestMethod]
    public void CatalogContainsAllThirteenSubstatsAndAllSixElements()
    {
        Assert.AreEqual(20, EchoStatCatalog.All.Count);
        Assert.AreEqual(13, EchoStatCatalog.All.Count(x => x.SubMaximum > 0));
        foreach (var id in new[] { "glacioDamage", "fusionDamage", "electroDamage", "aeroDamage", "spectroDamage", "havocDamage" })
        { Assert.AreEqual(30m, EchoStatCatalog.MainMaximum(id, EntryType.Percentage, 3)); Assert.AreEqual(0m, EchoStatCatalog.MainMaximum(id, EntryType.Percentage, 4)); }
        Assert.AreEqual(20, EchoStatCatalog.All.Select(x => (x.Id, x.Type)).Distinct().Count());
    }
    [TestMethod]
    public void EveryPresetValidatesAndGetsIndependentCopies()
    {
        foreach (var group in CharacterScorePresets.All)
        {
            Calculator.ValidateTemplate(group);
            Assert.AreEqual(2, group.ScoringVersion);
            Assert.AreEqual(13, group.Items.Count(x => x.Usage == EntryUsage.SubStatScoring));
            Assert.IsTrue(group.Items.All(x => x.PenaltyCoefficient.GetValueOrDefault() == 0));
            StringAssert.StartsWith(group.SourceUrl, "https://www.prydwen.gg/");
        }
        var one = CharacterScorePresets.Camellya(); Rule(one, "critRate").Weight = 0;
        Assert.AreEqual(1m, Rule(CharacterScorePresets.Camellya(), "critRate").Weight);
        Assert.AreEqual("1304", CharacterScorePresets.Jinhsi().RoleId);
        Assert.AreEqual("1505", CharacterScorePresets.Shorekeeper().RoleId);
    }
    [TestMethod]
    public void HighestLegalFiveAlwaysMakeOneHundred()
    {
        var group = CharacterScorePresets.Camellya();
        var result = calculator.ScoreEcho(group, Echo(Stat("critRate", 10.5m), Stat("critDamage", 21), Stat("atk", 11.6m), Stat("basicDamage", 11.6m), Stat("energyRegen", 12.4m)));
        Assert.AreEqual(4.1m, result.SubStats.MaximumBenefit);
        Assert.AreEqual(100m, result.SubStats.Score);
        Assert.AreEqual(100m, result.MainStats.Score);
        Assert.AreEqual(100m, result.TotalScore);
    }
    [TestMethod]
    public void DenominatorDoesNotShrinkToCurrentEffectiveEntries()
    {
        var result = calculator.ScoreEcho(CharacterScorePresets.Camellya(), Echo(Stat("critRate", 10.5m)));
        Assert.AreEqual(4.1m, result.SubStats.MaximumBenefit);
        Assert.AreEqual(100m / 4.1m, result.SubStats.Score, 0.00000001m);
    }
    [TestMethod]
    public void UselessDefenseRollDoesNotReduceTheScore()
    {
        var group = CharacterScorePresets.Camellya();
        var low = calculator.ScoreEcho(group, Echo(Stat("critRate", 8.4m), Stat("def", 8.1m)));
        var high = calculator.ScoreEcho(group, Echo(Stat("critRate", 8.4m), Stat("def", 14.7m)));
        Assert.AreEqual(low.TotalScore, high.TotalScore);
        Assert.AreEqual(0m, high.SubStats.Penalty);
    }
    [TestMethod]
    public void CoefficientConvertsValueRatherThanDeducting()
    {
        var group = CharacterScorePresets.Camellya();
        Rule(group, "atk").Weight = 0.5m;
        var result = calculator.ScoreEcho(group, Echo(Stat("atk", 5.8m)));
        Assert.AreEqual(0.25m, result.SubStats.Benefit);
        Assert.AreEqual(0m, result.SubStats.Penalty);
    }
    [TestMethod]
    public void NumericRollIncreaseIsMonotonicAndFixedAttackIsSeparate()
    {
        var group = CharacterScorePresets.Camellya();
        var low = calculator.ScoreEcho(group, Echo(Stat("atk", 30, EntryType.Value), Stat("atk", 6.4m)));
        var high = calculator.ScoreEcho(group, Echo(Stat("atk", 60, EntryType.Value), Stat("atk", 11.6m)));
        Assert.IsTrue(high.TotalScore > low.TotalScore);
        Assert.AreEqual(0.3m, high.SubStats.Details.Single(x => x.Type == EntryType.Value).Benefit);
    }
    [DataTestMethod]
    [DataRow(1, "atk", 18.0)]
    [DataRow(3, "spectroDamage", 30.0)]
    [DataRow(4, "critDamage", 44.0)]
    public void MainStatsUseCostSpecificMaximum(int cost, string stat, double max)
    {
        var group = CharacterScorePresets.Jinhsi();
        var result = calculator.ScoreEcho(group, new() { Cost = cost, MainStats = [Stat(stat, (decimal)max)] });
        Assert.AreEqual(100m, result.MainStats.Score);
    }
    [TestMethod]
    public void UnenhancedAndMissingSubstatsAreNotScoredAsPerfect()
    {
        var result = calculator.ScoreEcho(CharacterScorePresets.Camellya(), new() { Cost = 4, Level = 0, MainStats = [Stat("critRate", 4.4m)] });
        Assert.AreEqual(20m, result.MainStats.Score);
        Assert.AreEqual(0m, result.SubStats.Score);
    }
    [TestMethod]
    public void MainAndSubPercentagesStillSumToOneHundred()
    {
        var group = CharacterScorePresets.Camellya(); group.MainStatWeight = 0.2m; group.SubStatWeight = 0.8m;
        var result = calculator.ScoreEcho(group, Echo());
        Assert.AreEqual(20m, result.TotalScore);
        group.SubStatWeight = 0.7m;
        Assert.ThrowsException<ArgumentException>(() => calculator.ScoreEcho(group, Echo()));
    }
    [TestMethod]
    public void LegacyPenaltyIsRejectedRatherThanSilentlyReinterpreted()
    {
        var group = CharacterScorePresets.Camellya(); Rule(group, "def").PenaltyCoefficient = 0.3m;
        Assert.ThrowsException<ArgumentException>(() => calculator.ScoreEcho(group, Echo()));
    }
    [TestMethod]
    public void InvalidAttributesSlotsCostRarityAndRollsAreRejected()
    {
        var group = CharacterScorePresets.Camellya();
        var bad = new[]
        {
            Echo(Stat("critRate", 10.6m)), Echo(Stat("unknown", 1)), Echo(Stat("critRate", 5), Stat("critRate", 6)),
            Echo(Stat("critRate", 8), Stat("critDamage", 16), Stat("atk", 8), Stat("hp", 8), Stat("def", 8.1m), Stat("energyRegen", 8)),
            new EchoData { Cost = 2 }, new EchoData { Cost = 4, Rarity = 4 },
            new EchoData { Cost = 4, Level = 0, SubStats = [Stat("critRate", 8)] },
            new EchoData { Cost = 1, MainStats = [Stat("critRate", 22)] },
            new EchoData { Cost = 4, MainStats = [Stat("critRate", 22), Stat("critDamage", 44)] }
        };
        foreach (var echo in bad) Assert.ThrowsException<ArgumentException>(() => calculator.ScoreEcho(group, echo));
    }
    [TestMethod]
    public void ZeroWeightedSectionIsSupportedButPositiveSectionNeedsUsefulRules()
    {
        var group = CharacterScorePresets.Camellya();
        foreach (var rule in group.Items.Where(x => x.Usage == EntryUsage.SubStatScoring)) rule.Weight = 0;
        Assert.ThrowsException<ArgumentException>(() => calculator.ScoreEcho(group, Echo()));
        group.MainStatWeight = 1; group.SubStatWeight = 0;
        Assert.AreEqual(100m, calculator.ScoreEcho(group, Echo()).TotalScore);
    }
    [TestMethod]
    public void DevelopmentThresholdStaysIndependentAndStrict()
    {
        var group = CharacterScorePresets.Shorekeeper();
        var target = group.Items.Single(x => x.Usage == EntryUsage.DevelopmentTarget);
        target.TargetValue = 260; target.Comparison = ComparisonOperator.GreaterThan;
        Assert.IsFalse(calculator.EvaluateDevelopment(group, new()).IsComplete);
        Assert.IsFalse(calculator.EvaluateDevelopment(group, new() { Stats = [Stat("energyRegen", 260)] }).IsComplete);
        Assert.IsTrue(calculator.EvaluateDevelopment(group, new() { Stats = [Stat("energyRegen", 260.01m)] }).IsComplete);
    }
    [TestMethod]
    public void RealCamellyaSnapshotMatchesHandCalculationAndRoundTrips()
    {
        // 2026-10-19 当前库街区账号读取的赫卡忒；仅装备属性，不含账号数据。
        var echo = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures-camellya-live.json")), ScoringJsonContext.Default.EchoData)!;
        var group = CharacterScorePresets.Camellya();
        var expected = (17.4m / 21 + 9.3m / 10.5m + 11.6m / 11.6m * 0.8m + 7.9m / 11.6m * 0.8m) / 4.1m * 100;
        var result = calculator.ScoreEcho(group, echo);
        Assert.AreEqual(expected, result.SubStats.Score, 0.00000001m);
        Assert.AreEqual(30 + expected * 0.7m, result.TotalScore, 0.00000001m);
        var clone = JsonSerializer.Deserialize(JsonSerializer.Serialize(group, ScoringJsonContext.Default.EntryGroup), ScoringJsonContext.Default.EntryGroup)!;
        Assert.AreEqual(result.TotalScore, calculator.ScoreEcho(clone, echo).TotalScore);
    }
    [TestMethod]
    public void AdapterMapsCommonAliasesAndKeepsUnknownVisible()
    {
        var group = CharacterScorePresets.Camellya();
        var stats = EchoStatAdapter.Convert([("暴击", "９.３％"), ("攻击", "1,500"), ("新属性", "10%")], group, EntryUsage.MainStatScoring);
        Assert.AreEqual("critRate", stats[0].EntryNo);
        Assert.AreEqual("atk", stats[1].EntryNo);
        Assert.AreEqual(1500m, stats[1].Value);
        StringAssert.StartsWith(stats[2].EntryNo, "unmapped:");
    }
}
