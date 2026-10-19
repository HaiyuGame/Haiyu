using System.Text.Json.Serialization;
using ScoringService.Models;

namespace ScoringService;

/// <summary>模板、实际输入及评估结果的 JSON 源生成序列化上下文，枚举以名称存储。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(EntryGroup))]
[JsonSerializable(typeof(EntryItem))]
[JsonSerializable(typeof(List<EntryGroup>))]
[JsonSerializable(typeof(List<EntryItem>))]
[JsonSerializable(typeof(StatValue))]
[JsonSerializable(typeof(EchoData))]
[JsonSerializable(typeof(CharacterPanel))]
[JsonSerializable(typeof(EntryScoreDetail))]
[JsonSerializable(typeof(StatScoreResult))]
[JsonSerializable(typeof(EchoScoreResult))]
[JsonSerializable(typeof(DevelopmentTargetResult))]
[JsonSerializable(typeof(DevelopmentResult))]
[JsonSerializable(typeof(CharacterEvaluationResult))]
public partial class ScoringJsonContext : JsonSerializerContext
{
}
