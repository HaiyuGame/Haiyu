using System.Text.Json.Serialization;

namespace Waves.Core.Models.Downloader;

public class IndexChunkInfo
{
    [JsonPropertyName("start")]
    public long Start { get; set; }

    [JsonPropertyName("end")]
    public long End { get; set; }

    [JsonPropertyName("md5")]
    public string Md5 { get; set; }
}

public class IndexResource
{
    [JsonPropertyName("dest")]
    public string Dest { get; set; }

    [JsonPropertyName("md5")]
    public string Md5 { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("chunkInfos")]
    public List<IndexChunkInfo> ChunkInfos { get; set; }

    [JsonPropertyName("start")]
    public long? Start { get; set; }

    [JsonPropertyName("end")]
    public long? End { get; set; }

    [JsonPropertyName("fromFolder")]
    public string? FromFolder { get; set; }
}

[JsonSerializable(typeof(IndexGameResource))]
[JsonSerializable(typeof(IndexResource))]
[JsonSerializable(typeof(IndexChunkInfo))]
public partial class IndexGameResourceContext : JsonSerializerContext { }

public class IndexGameResource
{
    [JsonPropertyName("resource")]
    public List<IndexResource> Resource { get; set; }
}
