using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Waves.Api.Models;

[JsonSerializable(typeof(CdnList))]
[JsonSerializable(typeof(List<CdnList>))]
[JsonSerializable(typeof(Changelog))]
[JsonSerializable(typeof(CurrentGameInfo))]
[JsonSerializable(typeof(IndexDefault))]
[JsonSerializable(typeof(Download))]
[JsonSerializable(typeof(Experiment))]
[JsonSerializable(typeof(PreviousGameInfo))]
[JsonSerializable(typeof(ResourceChunk))]
[JsonSerializable(typeof(ResourcesDiff))]
[JsonSerializable(typeof(ResourcesGray))]
[JsonSerializable(typeof(ResourcesLogin))]
[JsonSerializable(typeof(RHIOptionList))]
[JsonSerializable(typeof(WavesIndex))]
[JsonSerializable(typeof(Text))]
[JsonSerializable(typeof(Predownload))]
public partial class WavesIndexContext : JsonSerializerContext { }

// Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);
public class CdnList
{
    [JsonPropertyName("K1")]
    public int K1 { get; set; }

    [JsonPropertyName("K2")]
    public int K2 { get; set; }

    [JsonPropertyName("P")]
    public int P { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}

public class Changelog
{
    [JsonPropertyName("zh-Hans")]
    public string ZhHans { get; set; }
}

public class CurrentGameInfo
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; }

    [JsonPropertyName("md5")]
    public string Md5 { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }
}

public class IndexDefault
{
    [JsonPropertyName("cdnList")]
    public List<CdnList> CdnList { get; set; }

    [JsonPropertyName("changelog")]
    public Changelog Changelog { get; set; }

    [JsonPropertyName("changelogVisible")]
    public int ChangelogVisible { get; set; }

    [JsonPropertyName("resourceChunk")]
    public ResourceChunk ResourceChunk { get; set; }

    [JsonPropertyName("resources")]
    public string Resources { get; set; }

    [JsonPropertyName("resourcesBasePath")]
    public string ResourcesBasePath { get; set; }

    [JsonPropertyName("resourcesDiff")]
    public ResourcesDiff ResourcesDiff { get; set; }

    [JsonPropertyName("resourcesExcludePath")]
    public List<object> ResourcesExcludePath { get; set; }

    [JsonPropertyName("resourcesExcludePathNeedUpdate")]
    public List<object> ResourcesExcludePathNeedUpdate { get; set; }

    [JsonPropertyName("sampleHashSwitch")]
    public int SampleHashSwitch { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }
}

public class Download
{
    [JsonPropertyName("dropNetworkError")]
    public int DropNetworkError { get; set; }

    [JsonPropertyName("disabledCompressed")]
    public int DisabledCompressed { get; set; }

    [JsonPropertyName("dropWrongContentLength")]
    public int DropWrongContentLength { get; set; }

    [JsonPropertyName("dropWrongContentEncoding")]
    public int DropWrongContentEncoding { get; set; }
}

public class Experiment
{
    [JsonPropertyName("download")]
    public Download Download { get; set; }
}

public class PreviousGameInfo
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; }

    [JsonPropertyName("md5")]
    public string Md5 { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }
}

public class ResourceChunk
{
    [JsonPropertyName("lastMd5")]
    public string LastMd5 { get; set; }

    [JsonPropertyName("lastResourceChunkPath")]
    public string LastResourceChunkPath { get; set; }

    [JsonPropertyName("lastResources")]
    public string LastResources { get; set; }

    [JsonPropertyName("lastVersion")]
    public string LastVersion { get; set; }

    [JsonPropertyName("md5")]
    public string Md5 { get; set; }

    [JsonPropertyName("resourceChunkPath")]
    public string ResourceChunkPath { get; set; }
}

public class ResourcesDiff
{
    [JsonPropertyName("currentGameInfo")]
    public CurrentGameInfo CurrentGameInfo { get; set; }

    [JsonPropertyName("previousGameInfo")]
    public PreviousGameInfo PreviousGameInfo { get; set; }
}

public class ResourcesGray
{
    [JsonPropertyName("graySwitch")]
    public int GraySwitch { get; set; }
}

public class ResourcesLogin
{
    [JsonPropertyName("host")]
    public string Host { get; set; }

    [JsonPropertyName("loginSwitch")]
    public int LoginSwitch { get; set; }
}

public class RHIOptionList
{
    [JsonPropertyName("cmdOption")]
    public string CmdOption { get; set; }

    [JsonPropertyName("isShow")]
    public int IsShow { get; set; }

    [JsonPropertyName("text")]
    public Text Text { get; set; }
}

public class Predownload
{
    [JsonPropertyName("changelog")]
    public Changelog Changelog { get; set; }

    [JsonPropertyName("resourceChunk")]
    public ResourceChunk ResourceChunk { get; set; }

    [JsonPropertyName("resources")]
    public string Resources { get; set; }

    [JsonPropertyName("resourcesBasePath")]
    public string ResourcesBasePath { get; set; }

    [JsonPropertyName("resourcesDiff")]
    public ResourcesDiff ResourcesDiff { get; set; }

    [JsonPropertyName("resourcesExcludePath")]
    public List<object> ResourcesExcludePath { get; set; }

    [JsonPropertyName("resourcesExcludePathNeedUpdate")]
    public List<object> ResourcesExcludePathNeedUpdate { get; set; }

    [JsonPropertyName("sampleHashSwitch")]
    public int SampleHashSwitch { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("config")]
    public Config Config { get; set; }
}

public class WavesIndex
{
    [JsonPropertyName("hashCacheCheckAccSwitch")]
    public int HashCacheCheckAccSwitch { get; set; }

    [JsonPropertyName("default")]
    public IndexDefault Default { get; set; }

    [JsonPropertyName("predownloadSwitch")]
    public int PredownloadSwitch { get; set; }

    [JsonPropertyName("predownload")]
    public Predownload Predownload { get; set; }

    [JsonPropertyName("RHIOptionSwitch")]
    public int RHIOptionSwitch { get; set; }

    [JsonPropertyName("RHIOptionList")]
    public List<RHIOptionList> RHIOptionList { get; set; }

    [JsonPropertyName("resourcesLogin")]
    public ResourcesLogin ResourcesLogin { get; set; }

    [JsonPropertyName("checkExeIsRunning")]
    public int CheckExeIsRunning { get; set; }

    [JsonPropertyName("keyFileCheckSwitch")]
    public int KeyFileCheckSwitch { get; set; }

    [JsonPropertyName("keyFileCheckList")]
    public List<string> KeyFileCheckList { get; set; }

    [JsonPropertyName("chunkDownloadSwitch")]
    public int ChunkDownloadSwitch { get; set; }

    [JsonPropertyName("fingerprints")]
    public List<string> Fingerprints { get; set; }

    [JsonPropertyName("resourcesGray")]
    public ResourcesGray ResourcesGray { get; set; }

    [JsonPropertyName("experiment")]
    public Experiment Experiment { get; set; }
}

public class Text
{
    [JsonPropertyName("zh-Hans")]
    public string ZhHans { get; set; }

    [JsonPropertyName("de")]
    public string De { get; set; }

    [JsonPropertyName("zh-Hant")]
    public string ZhHant { get; set; }

    [JsonPropertyName("ko")]
    public string Ko { get; set; }

    [JsonPropertyName("ja")]
    public string Ja { get; set; }

    [JsonPropertyName("en")]
    public string En { get; set; }

    [JsonPropertyName("fr")]
    public string Fr { get; set; }

    [JsonPropertyName("es")]
    public string Es { get; set; }
}
