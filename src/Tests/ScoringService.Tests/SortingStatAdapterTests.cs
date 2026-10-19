using KuroGameDownloadProgram.Tests;
using ScoringService.Models;
using ScoringService.Models.Enums;

namespace ScoringService.Tests;

[TestClass]
public sealed class SortingStatAdapterTests
{
    [DataTestMethod]
    [DataRow("10%", EntryType.Percentage, "10")]
    [DataRow(" ２６０．０１％ ", EntryType.Percentage, "260.01")]
    [DataRow("24", EntryType.Value, "24")]
    [DataRow("1,234.50", EntryType.Value, "1234.50")]
    [DataRow("+8.5 %", EntryType.Percentage, "8.5")]
    public void ParsesApiValuesWithoutChangingPercentageScale(string raw, EntryType type, string value)
    {
        var result = SortingStatAdapter.ParseValue(raw);
        Assert.AreEqual(type, result.Type);
        Assert.AreEqual(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture), result.Value);
    }

    [DataTestMethod]
    [DataRow("--")]
    [DataRow("-1%")]
    [DataRow("1,2")]
    [DataRow("10%%")]
    [DataRow("NaN")]
    public void RejectsMalformedValuesInsteadOfScoringThemAsZero(string raw)
        => Assert.ThrowsException<FormatException>(() => SortingStatAdapter.ParseValue(raw));

    [TestMethod]
    public void MapsAliasesByUsageAndUnit()
    {
        var template = Template();
        var stats = SortingStatAdapter.Convert([("攻击", "8%"), ("攻击力", "24"), ("防御", "6%")],
            template, EntryUsage.SubStatScoring);
        Assert.AreEqual("atk", stats[0].EntryNo);
        Assert.AreEqual("atk", stats[1].EntryNo);
        Assert.AreEqual(EntryType.Value, stats[1].Type);
        Assert.AreEqual("def", stats[2].EntryNo);
        var main = SortingStatAdapter.Convert([("攻击力", "30%")], template, EntryUsage.MainStatScoring);
        Assert.AreEqual("main-atk", main.Single().EntryNo);
    }

    [TestMethod]
    public void KeepsUnknownAttributesVisibleAndDoesNotGuessPenalty()
    {
        var stats = SortingStatAdapter.Convert([("暴击", "10%")], Template(), EntryUsage.SubStatScoring);
        Assert.AreEqual("unmapped:暴击", stats.Single().EntryNo);
        Assert.AreEqual(10m, stats.Single().Value);
    }

    [TestMethod]
    public void AmbiguousTemplateNamesAreRejected()
    {
        var template = Template();
        template.Items.Add(new()
        {
            EntryNo = "second-atk", EntryName = "攻击力", Type = EntryType.Percentage,
            Usage = EntryUsage.SubStatScoring
        });
        Assert.ThrowsException<InvalidOperationException>(() =>
            SortingStatAdapter.Convert([("攻击", "8%")], template, EntryUsage.SubStatScoring));
    }

    [TestMethod]
    public void DuplicateApiPropertiesAreRejectedRatherThanAddedTwice()
        => Assert.ThrowsException<InvalidOperationException>(() =>
            SortingStatAdapter.Convert([("攻击", "8%"), ("攻击力加成", "9%")],
                Template(), EntryUsage.SubStatScoring));

    [TestMethod]
    public void ApiConversionAndScoringMatchHandCalculation()
    {
        var template = Template();
        var echo = new EchoData
        {
            MainStats = SortingStatAdapter.Convert([("攻击力", "30%")], template, EntryUsage.MainStatScoring),
            SubStats = SortingStatAdapter.Convert([("攻击", "8%"), ("攻击", "24"), ("防御", "6%")],
                template, EntryUsage.SubStatScoring)
        };
        var result = new global::ScoringService.ScoringService().ScoreEcho(template, echo);
        Assert.AreEqual(1.1m, result.SubStats.Benefit);
        Assert.AreEqual(0.18m, result.SubStats.Penalty);
        Assert.AreEqual(72.93333333m, result.TotalScore, 0.00000001m);
    }

    private static EntryGroup Template() => new()
    {
        ScoringVersion = 1, MainStatMaximumBenefit = 1, SubStatMaximumBenefit = 1.5m,
        Items =
        [
            new() { EntryNo = "main-atk", EntryName = "攻击力加成", Usage = EntryUsage.MainStatScoring,
                Type = EntryType.Percentage, ReferenceValue = 30, Weight = 1 },
            new() { EntryNo = "atk", EntryName = "攻击力加成", Usage = EntryUsage.SubStatScoring,
                Type = EntryType.Percentage, ReferenceValue = 10, Weight = 1 },
            new() { EntryNo = "atk", EntryName = "固定攻击力", Usage = EntryUsage.SubStatScoring,
                Type = EntryType.Value, ReferenceValue = 40, Weight = 0.5m },
            new() { EntryNo = "def", EntryName = "防御力加成", Usage = EntryUsage.SubStatScoring,
                Type = EntryType.Percentage, ReferenceValue = 10, PenaltyCoefficient = 0.3m }
        ]
    };
}
