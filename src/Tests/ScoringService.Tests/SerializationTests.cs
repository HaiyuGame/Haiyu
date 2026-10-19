using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScoringService.Models;
using ScoringService.Models.Enums;
using Calculator = ScoringService.ScoringService;

namespace ScoringService.Tests;

[TestClass]
public sealed class SerializationTests
{
    [TestMethod]
    public void EveryModelPropertyHasExplicitJsonNameAndSourceMetadata()
    {
        var models = typeof(EntryItem).Assembly.GetTypes()
            .Where(t => t.IsPublic && t.Namespace == "ScoringService.Models");
        foreach (var model in models)
        {
            Assert.IsNotNull(ScoringJsonContext.Default.GetTypeInfo(model), model.Name);
            foreach (var property in model.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();
                Assert.IsNotNull(attribute, $"{model.Name}.{property.Name}");
                Assert.AreEqual(JsonNamingPolicy.CamelCase.ConvertName(property.Name), attribute.Name);
            }
        }
    }

    [TestMethod]
    public void InputModelsRoundTripWithExplicitPropertyNames()
    {
        var echo = new EchoData
        {
            Id = "json-echo",
            MainStats = [new() { EntryNo = "atk", Type = EntryType.Percentage, Value = 30 }],
            SubStats = [new() { EntryNo = "atk", Type = EntryType.Value, Value = 24 }]
        };
        var json = JsonSerializer.Serialize(echo, ScoringJsonContext.Default.EchoData);
        using var document = JsonDocument.Parse(json);
        AssertNames(document.RootElement, "cost", "rarity", "level", "id", "mainStats", "subStats");
        var stat = document.RootElement.GetProperty("subStats")[0];
        AssertNames(stat, "entryNo", "type", "value");
        Assert.AreEqual("Value", stat.GetProperty("type").GetString());
        var restored = JsonSerializer.Deserialize(json, ScoringJsonContext.Default.EchoData)!;
        Assert.AreEqual(echo.Id, restored.Id);
        Assert.AreEqual(EntryType.Percentage, restored.MainStats.Single().Type);
        Assert.AreEqual(24m, restored.SubStats.Single().Value);

        var panel = new CharacterPanel
        {
            Stats = [new() { EntryNo = "recharge", Type = EntryType.Percentage, Value = 260.01m }]
        };
        var panelJson = JsonSerializer.Serialize(panel, ScoringJsonContext.Default.CharacterPanel);
        var restoredPanel = JsonSerializer.Deserialize(panelJson, ScoringJsonContext.Default.CharacterPanel)!;
        Assert.AreEqual(260.01m, restoredPanel.Stats.Single().Value);
    }

    [TestMethod]
    public void ResultRecordsRoundTripIncludingComputedProperties()
    {
        var template = LoadTemplate();
        var echo = new EchoData
        {
            MainStats = [new() { EntryNo = "atk", Type = EntryType.Percentage, Value = 30 }],
            SubStats = [new() { EntryNo = "def", Type = EntryType.Percentage, Value = 6 }]
        };
        var result = new Calculator().Evaluate(template, echo, new()
        {
            Stats = [new() { EntryNo = "recharge", Type = EntryType.Percentage, Value = 260.01m }]
        });
        var json = JsonSerializer.Serialize(result, ScoringJsonContext.Default.CharacterEvaluationResult);
        using var document = JsonDocument.Parse(json);
        AssertNames(document.RootElement, "echoScore", "development");
        var echoJson = document.RootElement.GetProperty("echoScore");
        AssertNames(echoJson, "echoId", "mainStats", "subStats", "totalScore");
        var subJson = echoJson.GetProperty("subStats");
        AssertNames(subJson, "score", "maximumBenefit", "details", "benefit", "penalty");
        AssertNames(subJson.GetProperty("details")[0], "entryNo", "type", "value", "isMatched",
            "normalizedValue", "benefit", "penalty", "netContribution");
        var developmentJson = document.RootElement.GetProperty("development");
        AssertNames(developmentJson, "targets", "isConfigured", "metCount", "isComplete");
        AssertNames(developmentJson.GetProperty("targets")[0], "entryNo", "entryName", "type",
            "actualValue", "targetValue", "comparison", "isMet");

        var restored = JsonSerializer.Deserialize(json, ScoringJsonContext.Default.CharacterEvaluationResult)!;
        Assert.AreEqual(result.EchoScore.TotalScore, restored.EchoScore.TotalScore);
        Assert.AreEqual(0.18m, restored.EchoScore.SubStats.Penalty);
        Assert.AreEqual(-0.18m, restored.EchoScore.SubStats.Details.Single().NetContribution);
        Assert.AreEqual(ComparisonOperator.GreaterThan, restored.Development.Targets.Single().Comparison);
        Assert.AreEqual(260.01m, restored.Development.Targets.Single().ActualValue);
        Assert.AreEqual(1, restored.Development.MetCount);
        Assert.IsTrue(restored.Development.IsComplete);
    }

    [TestMethod]
    public void MissingPanelValuesRemainNullAfterRoundTrip()
    {
        var result = new Calculator().EvaluateDevelopment(LoadTemplate(), new());
        var json = JsonSerializer.Serialize(result, ScoringJsonContext.Default.DevelopmentResult);
        var restored = JsonSerializer.Deserialize(json, ScoringJsonContext.Default.DevelopmentResult)!;
        Assert.IsNull(restored.Targets.Single().ActualValue);
        Assert.IsFalse(restored.IsComplete);
    }

    [TestMethod]
    public void ExplicitNamesOverrideSerializerNamingPolicy()
    {
        var context = new ScoringJsonContext(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        var json = JsonSerializer.Serialize(LoadTemplate(), context.EntryGroup);
        using var document = JsonDocument.Parse(json);
        Assert.IsTrue(document.RootElement.TryGetProperty("mainStatMaximumBenefit", out _));
        Assert.IsFalse(document.RootElement.TryGetProperty("main_stat_maximum_benefit", out _));
        Assert.IsTrue(document.RootElement.GetProperty("items")[0].TryGetProperty("entryNo", out _));
    }

    private static EntryGroup LoadTemplate()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "basic-template.json"));
        return JsonSerializer.Deserialize(json, ScoringJsonContext.Default.EntryGroup)!;
    }

    private static void AssertNames(JsonElement element, params string[] names)
        => CollectionAssert.AreEquivalent(names, element.EnumerateObject().Select(x => x.Name).ToArray());
}
