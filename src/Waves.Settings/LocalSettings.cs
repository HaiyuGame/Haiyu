using System.Text.Json.Serialization;

namespace Waves.Settings;

/// <summary>
/// 通用 key-value 配置项（磁盘 JSON 存储格式）。
/// </summary>
public sealed class LocalSettings
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

[JsonSerializable(typeof(LocalSettings))]
[JsonSerializable(typeof(List<LocalSettings>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class LocalSettingsJsonContext : JsonSerializerContext;
