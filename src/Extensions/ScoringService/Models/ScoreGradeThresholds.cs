using System.Text.Json.Serialization;
namespace ScoringService.Models;

/// <summary>角色自定义的百分制门槛；默认值为工具参考值，不代表游戏官方评价。</summary>
public sealed class ScoreGradeThresholds
{
    /// <summary>SSS最低分。</summary>
    [JsonPropertyName("sss")] public decimal SSS { get; set; } = 95;
    /// <summary>SS最低分。</summary>
    [JsonPropertyName("ss")] public decimal SS { get; set; } = 85;
    /// <summary>S最低分。</summary>
    [JsonPropertyName("s")] public decimal S { get; set; } = 75;
    /// <summary>A最低分。</summary>
    [JsonPropertyName("a")] public decimal A { get; set; } = 60;
    /// <summary>B最低分；更低为C。</summary>
    [JsonPropertyName("b")] public decimal B { get; set; } = 40;
    /// <summary>声骸配装毕业最低分，不代表角色面板目标已经完成。</summary>
    [JsonPropertyName("graduation")] public decimal Graduation { get; set; } = 85;
}

/// <summary>毕业评分与理论质量分的并列结果；进度可超过100，展示分封顶100。</summary>
public sealed record GraduationScoreResult(
    [property: JsonPropertyName("score")] EchoScoreResult Score,
    [property: JsonPropertyName("progress")] decimal Progress,
    [property: JsonPropertyName("theoreticalScore")] decimal TheoreticalScore);
