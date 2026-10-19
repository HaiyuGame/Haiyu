using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Haiyu.Model;
using ScoringService.Models;

namespace ScoringService.Tests;

[TestClass]
public sealed class SortingTemplateCardTests
{
    [TestMethod]
    public void IsSelectRaisesPropertyChangedForTwoWaySelection()
    {
        var card = new SortingTemplateCard(new());
        var notifications = new List<string?>();
        card.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        card.IsSelect = true;
        card.IsSelect = true;
        card.IsSelect = false;
        CollectionAssert.AreEqual(new[] { nameof(card.IsSelect), nameof(card.IsSelect) }, notifications);
        Assert.IsFalse(card.IsSelect);
        Assert.IsInstanceOfType<INotifyPropertyChanged>(card);
    }

    [TestMethod]
    public void SelectedCardNeverWritesItsSelectionStateToJson()
    {
        var card = new SortingTemplateCard(new() { Id = "selected-template", Name = "模板" }) { IsSelect = true };
        var json = JsonSerializer.Serialize(card, SortingCardJsonContext.Default.SortingTemplateCard);
        using var document = JsonDocument.Parse(json);
        Assert.IsFalse(document.RootElement.TryGetProperty("isSelect", out _));
        Assert.IsFalse(document.RootElement.TryGetProperty("IsSelect", out _));
        Assert.AreEqual("selected-template", document.RootElement.GetProperty("model").GetProperty("id").GetString());
        var restored = JsonSerializer.Deserialize(json, SortingCardJsonContext.Default.SortingTemplateCard)!;
        Assert.IsFalse(restored.IsSelect);
        Assert.AreEqual(card.Model.Id, restored.Model.Id);
    }

    [TestMethod]
    public void IncomingSelectionFlagIsIgnoredAndExportedTemplateIsUnchanged()
    {
        var card = JsonSerializer.Deserialize(
            """{"model":{"id":"test"},"isSelect":true}""",
            SortingCardJsonContext.Default.SortingTemplateCard)!;
        Assert.IsFalse(card.IsSelect);
        var before = JsonSerializer.Serialize(card.Model, ScoringJsonContext.Default.EntryGroup);
        card.IsSelect = true;
        Assert.AreEqual(before, JsonSerializer.Serialize(card.Model, ScoringJsonContext.Default.EntryGroup));
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SortingTemplateCard))]
internal partial class SortingCardJsonContext : JsonSerializerContext;
