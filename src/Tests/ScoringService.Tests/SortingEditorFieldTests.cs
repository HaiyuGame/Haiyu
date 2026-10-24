using Haiyu.ViewModels.SortingViewModels;
using ScoringService.Models.Enums;

namespace ScoringService.Tests;

[TestClass]
public sealed class SortingEditorFieldTests
{
    [TestMethod]
    public void NormalizationIsVisibleAndUsesSubstatRatherThanMainStatMaximum()
    {
        var stat = EchoStatCatalog.Find("critRate", EntryType.Percentage)!;
        var sub = new SortingEntryViewModel(EchoStatCatalog.Rule(stat, EntryUsage.SubStatScoring));
        var main = new SortingEntryViewModel(EchoStatCatalog.Rule(stat, EntryUsage.MainStatScoring));
        StringAssert.Contains(sub.Limit, "10.5%");
        StringAssert.Contains(sub.Limit, "实际值 ÷");
        StringAssert.Contains(main.Limit, "4C 22%");
        Assert.IsFalse(main.Limit.Contains("10.5%"));
    }

    [TestMethod]
    public void SliderAndNumericInputUpdateTheSameRuleAndRejectNan()
    {
        var rule = EchoStatCatalog.Rule(EchoStatCatalog.Find("atk", EntryType.Percentage)!, EntryUsage.SubStatScoring, 0.8m);
        var row = new SortingEntryViewModel(rule);
        int changed = 0;
        row.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(row.Coefficient)) changed++; };
        row.Coefficient = 0.35;
        Assert.AreEqual(0.35m, rule.Weight);
        Assert.AreEqual(0.35, row.Coefficient);
        row.Coefficient = double.NaN;
        row.Coefficient = double.PositiveInfinity;
        Assert.AreEqual(0.35m, rule.Weight);
        Assert.AreEqual(1, changed);
        row.Coefficient = 2; Assert.AreEqual(1m, rule.Weight);
        row.Coefficient = -1; Assert.AreEqual(0m, rule.Weight);
    }

    [TestMethod]
    public void TargetUsesManualPanelAndStrictBoundaryWithoutChangingScoreParameters()
    {
        var rule = EchoStatCatalog.Rule(EchoStatCatalog.Find("energyRegen", EntryType.Percentage)!, EntryUsage.DevelopmentTarget);
        rule.TargetValue = 260; rule.Comparison = ComparisonOperator.GreaterThan;
        var row = new SortingTargetViewModel(rule);
        Assert.AreEqual("暂无面板", row.Status);
        row.Actual = 260; Assert.AreEqual("未达标", row.Status);
        row.Actual = 260.01; Assert.AreEqual("已达标", row.Status);
        Assert.IsNull(rule.Weight);
        Assert.IsNull(rule.ReferenceValue);
        row.Actual = double.NaN; Assert.AreEqual("暂无面板", row.Status);
    }
}
