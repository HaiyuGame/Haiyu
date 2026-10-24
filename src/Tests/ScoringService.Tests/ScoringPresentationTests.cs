using System.Text.Json;
using Haiyu.Model;
using ScoringService.Models;
using ScoringService.Models.Enums;
using Calculator = ScoringService.ScoringService;

namespace ScoringService.Tests;

[TestClass]
public sealed class ScoringPresentationTests
{
    [DataTestMethod]
    [DataRow(0, "C")]
    [DataRow(39.9, "C")]
    [DataRow(40, "B")]
    [DataRow(59.9, "B")]
    [DataRow(60, "A")]
    [DataRow(74.9, "A")]
    [DataRow(75, "S")]
    [DataRow(84.9, "S")]
    [DataRow(85, "SS")]
    [DataRow(94.9, "SS")]
    [DataRow(95, "SSS")]
    [DataRow(100, "SSS")]
    public void RatingThresholdsMatchDisplayedScore(double score, string expected)
    {
        Assert.AreEqual(expected, ScoreRatings.Grade((decimal)score));
        Assert.IsFalse(string.IsNullOrWhiteSpace(ScoreRatings.Comment((decimal)score)));
    }

    [TestMethod]
    public void EffectiveHitsCountPositiveBenefitsNotMerelyMatchingRules()
    {
        var echo = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures-camellya-live.json")), ScoringJsonContext.Default.EchoData)!;
        var score = new Calculator().ScoreEcho(CharacterScorePresets.Camellya(), echo);
        var card = new SortingEchoCard("赫卡忒", echo, score);
        Assert.AreEqual(5, score.SubStats.Details.Count(x => x.IsMatched));
        Assert.AreEqual(4, card.EffectiveCount);
        Assert.AreEqual("有效词条 4/5", card.HitSummary);
        Assert.IsFalse(card.StatDetails.Single(x => x.Usage == "副词条" && x.Name == "生命值").IsEffective);
        StringAssert.Contains(card.CopyText, card.Grade);
    }

    [TestMethod]
    public void ApiPanelFieldsDeserializeWithSourceGenerationAndFeedDevelopmentTargets()
    {
        // 当前真实响应的相关字段，剔除账号、装备与其他无关内容。
        const string json = """
        {"roleAttributeList":[
          {"attributeName":"生命","attributeValue":"37274","iconUrl":""},
          {"attributeName":"共鸣效率","attributeValue":"192.2%","iconUrl":""}],
         "roleSkin":{"picUrl":"https://example.com/portrait.png","skinIcon":"https://example.com/icon.png"}}
        """;
        var detail = JsonSerializer.Deserialize(json, Waves.Api.Models.CommunityContext.Default.GamerRoilDetily)!;
        Assert.AreEqual(2, detail.RoleAttributeList!.Count);
        Assert.AreEqual("https://example.com/portrait.png", detail.RoleSkin!.PicUrl);
        var group = CharacterScorePresets.Shorekeeper();
        var panel = new CharacterPanel { Stats = EchoStatAdapter.Convert(detail.RoleAttributeList.Select(x => (x.AttributeName, x.AttributeValue)), group, EntryUsage.DevelopmentTarget) };
        Assert.AreEqual(37274m, panel.Stats.Single(x => x.EntryNo == "hp").Value);
        var result = new Calculator().EvaluateDevelopment(group, panel);
        Assert.AreEqual(192.2m, result.Targets.Single().ActualValue);
        Assert.IsFalse(result.IsComplete);
    }
}
