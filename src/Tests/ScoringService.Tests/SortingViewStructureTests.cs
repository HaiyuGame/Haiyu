using System.Xml.Linq;

namespace ScoringService.Tests;

/// <summary>检查源 XAML 的交互约定，补充选中状态和计算测试；不替代实际界面检查。</summary>
[TestClass]
public sealed class SortingViewStructureTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static XDocument Read(string file) => XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", file));

    private static XElement Element(XDocument document, string id) => document.Descendants()
        .Single(x => (string?)x.Attribute("AutomationProperties.AutomationId") == id);

    [TestMethod]
    public void TemplatesUseItemsViewAndTwoWayIsSelect()
    {
        var view = Element(Read("SortingManagerPage.xaml"), "SortingTemplates");
        Assert.AreEqual("ItemsView", view.Name.LocalName);
        Assert.AreEqual("Single", (string?)view.Attribute("SelectionMode"));
        Assert.AreEqual("Template_SelectionChanged", (string?)view.Attribute("SelectionChanged"));
        var container = view.Descendants(Presentation + "ItemContainer").Single();
        var selected = (string)container.Attribute("IsSelected")!;
        StringAssert.Contains(selected, "IsSelect");
        StringAssert.Contains(selected, "Mode=TwoWay");
    }

    [TestMethod]
    public void NewAndImportEntryActionsOnlyAppearInLeftManagementHeader()
    {
        var document = Read("SortingManagerPage.xaml");
        foreach (var id in new[] { "SortingCreateEntry", "SortingImportEntry" })
        {
            var button = Element(document, id);
            Assert.AreEqual("Button", button.Name.LocalName);
            Assert.IsFalse(button.Ancestors(Presentation + "CommandBar").Any());
            Assert.IsTrue(button.Ancestors(Presentation + "Border").Any(x =>
                x.Descendants().Any(y => (string?)y.Attribute("AutomationProperties.AutomationId") == "SortingMainEntries")));
        }
    }

    [TestMethod]
    public void AllSavedEntryCategoriesUseBoundListViews()
    {
        var document = Read("SortingManagerPage.xaml");
        foreach (var (id, collection) in new[]
        {
            ("SortingMainEntries", "MainEntries"),
            ("SortingSubEntries", "SubEntries"),
            ("SortingTargetEntries", "Targets")
        })
        {
            var list = Element(document, id);
            Assert.AreEqual("ListView", list.Name.LocalName);
            StringAssert.Contains((string)list.Attribute("ItemsSource")!, $"ViewModel.{collection}");
            Assert.IsTrue(list.Parent!.Elements(Presentation + "TextBlock").Any(x =>
                ((string?)x.Attribute("Visibility"))?.Contains($"ViewModel.{collection}.Count", StringComparison.Ordinal) == true));
        }
    }

    [TestMethod]
    public void EntryRowsUseOneLineWithEllipsisAndFullTooltip()
    {
        var document = Read("SortingManagerPage.xaml");
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var template = document.Descendants(Presentation + "DataTemplate")
            .Single(x => (string?)x.Attribute(xaml + "Key") == "EntryTemplate");
        var text = template.Elements().Single();
        Assert.AreEqual("TextBlock", text.Name.LocalName);
        Assert.AreEqual("1", (string?)text.Attribute("MaxLines"));
        Assert.AreEqual("NoWrap", (string?)text.Attribute("TextWrapping"));
        Assert.AreEqual("CharacterEllipsis", (string?)text.Attribute("TextTrimming"));
        Assert.AreEqual("{x:Bind DisplayText}", (string?)text.Attribute("Text"));
        Assert.AreEqual((string?)text.Attribute("Text"), (string?)text.Attribute("ToolTipService.ToolTip"));
    }

    [TestMethod]
    public void EntryDisplayTextSeparatesAllDetailsWithMiddleDots()
    {
        var card = new Haiyu.Model.SortingEntryCard(new global::ScoringService.Models.EntryItem
        {
            EntryName = "攻击力",
            EntryNo = "power",
            Type = global::ScoringService.Models.Enums.EntryType.Percentage,
            Usage = global::ScoringService.Models.Enums.EntryUsage.MainStatScoring,
            ReferenceValue = 10,
            Weight = 1,
            PenaltyCoefficient = 0
        });
        Assert.AreEqual("攻击力 · power · 百分比 · 主词条 · 基准 10 · 权重 1 · 惩罚 0", card.DisplayText);
        Assert.IsFalse(card.DisplayText.Contains('\n'));
    }

    [TestMethod]
    public void EveryEntryInputHasAnExplanationAndScoringExamples()
    {
        var document = Read("SortingEntryDialog.xaml");
        foreach (var suffix in new[] { "Id", "Name", "Type", "Usage", "Reference", "Weight", "Penalty", "Target", "Comparison" })
        {
            var input = Element(document, "SortingEntry" + suffix);
            var help = (string?)input.Attribute("AutomationProperties.HelpText");
            Assert.IsFalse(string.IsNullOrWhiteSpace(help), suffix);
            Assert.IsTrue(document.Descendants(Presentation + "TextBlock").Any(x => (string?)x.Attribute("Text") == help), suffix);
        }
        StringAssert.Contains((string)Element(document, "SortingEntryReference").Attribute("AutomationProperties.HelpText")!, "8 ÷ 10 = 0.8");
        StringAssert.Contains((string)Element(document, "SortingEntryWeight").Attribute("AutomationProperties.HelpText")!, "8 ÷ 10 × 0.5 = 0.4");
        StringAssert.Contains((string)Element(document, "SortingEntryPenalty").Attribute("AutomationProperties.HelpText")!, "6 ÷ 10 × 0.3 = 0.18");
    }
}
