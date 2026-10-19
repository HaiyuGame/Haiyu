using System.Text.Json.Serialization;

namespace ScoringService.Models;

/// <summary>评分模板与词条的本地存储目录配置。</summary>
public sealed class SortingOption
{
    /// <summary>角色评分模板 JSON 文件所在目录。</summary>
    [JsonPropertyName("groupFolder")]
    public string GroupFolder { get; set; }

    /// <summary>独立词条 JSON 文件所在目录。</summary>
    [JsonPropertyName("itemFolder")]
    public string ItemFolder { get; set; }
}
