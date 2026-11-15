namespace Waves.Core.Models.Downloader;

public sealed class ResourceTagRule
{
    [JsonPropertyName("prefixes")]
    public List<string>? Prefixes { get; set; }

    [JsonPropertyName("exact")]
    public List<string>? Exact { get; set; }

    [JsonPropertyName("exclude_prefixes")]
    public List<string>? ExcludePrefixes { get; set; }
}
