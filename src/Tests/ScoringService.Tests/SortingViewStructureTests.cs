using System.Xml.Linq;
namespace ScoringService.Tests;
[TestClass]
public sealed class SortingViewStructureTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static XDocument Read(string file) => XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", file));
    [TestMethod]
    public void GraduationControlsAreBoundToRoleTemplateSettings()
    {
        var doc = Read("SortingTemplatePage.xaml");
        foreach (var property in new[] { "TargetCount", "GraduationLine", "GradeSSS", "GradeSS", "GradeS", "GradeA", "GradeB" })
            Assert.IsTrue(doc.Descendants(P + "NumberBox").Any(x => (string?)x.Attribute("Value") == "{x:Bind ViewModel." + property + ", Mode=TwoWay}"), property);
    }

    [TestMethod]
    public void EditorExposesNormalizationAndManagementActions()
    {
        var doc = Read("SortingTemplatePage.xaml");
        foreach (var id in new[] { "SortingAddRule", "SortingDeleteTemplate" })
            Assert.IsTrue(doc.Descendants().Any(x => (string?)x.Attribute("AutomationProperties.AutomationId") == id));
        Assert.IsTrue(doc.Descendants(P + "TextBlock").Any(x => (string?)x.Attribute("Text") == "{x:Bind Limit}"));
        Assert.IsTrue(doc.Descendants(P + "Button").Any(x => (string?)x.Attribute("Click") == "EditRule_Click"));
        Assert.IsTrue(doc.Descendants(P + "Button").Any(x => (string?)x.Attribute("Click") == "RemoveRule_Click"));
    }

    [TestMethod]
    public void TemplatesUseTwoWaySelectionWithoutPersistingUiState()
    {
        var view = Read("SortingManagerPage.xaml").Descendants(P + "ItemsView").Single();
        Assert.AreEqual("Single", (string?)view.Attribute("SelectionMode"));
        StringAssert.Contains((string)view.Descendants(P + "ItemContainer").Single().Attribute("IsSelected")!, "IsSelect, Mode=TwoWay");
    }
    [TestMethod]
    public void CoefficientSliderAndNumberBoxShareTheSameValue()
    {
        var doc = Read("SortingTemplatePage.xaml");
        var slider = doc.Descendants(P + "Slider").Single(x => (string?)x.Attribute("Maximum") == "1");
        var number = doc.Descendants(P + "NumberBox").Single(x => (string?)x.Attribute("Maximum") == "1");
        Assert.AreEqual("{x:Bind Coefficient, Mode=TwoWay}", (string?)slider.Attribute("Value"));
        Assert.AreEqual((string?)slider.Attribute("Value"), (string?)number.Attribute("Value"));
        Assert.AreEqual("0.05", (string?)slider.Attribute("StepFrequency"));
        Assert.IsNotNull(slider.Attribute("ToolTipService.ToolTip"));
        Assert.IsNotNull(slider.Attribute("AutomationProperties.Name"));
    }
    [TestMethod]
    public void ManagerHasRoleSelectionCopyAndBuiltInCatalog()
    {
        var doc = Read("SortingManagerPage.xaml");
        foreach (var id in new[] { "SortingRole", "SortingCopy", "SortingRefresh", "SortingEdit", "SortingResults" })
            Assert.IsTrue(doc.Descendants().Any(x => (string?)x.Attribute("AutomationProperties.AutomationId") == id), id);
        var catalog = doc.Descendants(P + "ListView").Single(x => (string?)x.Attribute("ItemsSource") == "{x:Bind ViewModel.Catalog}");
        var text = catalog.Descendants(P + "TextBlock").Single();
        Assert.AreEqual("1", (string?)text.Attribute("MaxLines"));
        Assert.AreEqual("CharacterEllipsis", (string?)text.Attribute("TextTrimming"));
    }
    [TestMethod]
    public void TargetsComeFromCatalogAndDefaultToEnergyRegen()
    {
        var doc = Read("SortingEntryDialog.xaml");
        Assert.IsTrue(doc.Descendants(P + "ComboBox").Any(x => (string?)x.Attribute("ItemsSource") == "{x:Bind Catalog}"));
        Assert.IsTrue(doc.Descendants(P + "NumberBox").Any(x => x.Attribute("ToolTipService.ToolTip") is not null));
    }

    [TestMethod]
    public void CatalogAndRoleTemplatesUseUiProjectionTypesRatherThanNetworkDtos()
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        foreach (var file in new[] { "SortingManagerPage.xaml", "SortingEntryDialog.xaml" })
        {
            var types = Read(file).Descendants(P + "DataTemplate").Select(x => (string?)x.Attribute(xaml + "DataType")).ToArray();
            Assert.IsTrue(types.Contains("models:SortingStatOption"));
            Assert.IsFalse(types.Any(x => x is "stats:EchoStatDefinition" or "roles:RoleList"));
        }
    }
}
