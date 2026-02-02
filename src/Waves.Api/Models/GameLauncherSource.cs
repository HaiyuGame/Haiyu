using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Waves.Api.Models;

[JsonSourceGenerationOptions(UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement)]
[JsonSerializable(typeof(GameLauncherSource))]
[JsonSerializable(typeof(AnimateBackground))]
[JsonSerializable(typeof(AnimateBackground))]
[JsonSerializable(typeof(AnimateBackgroundLanguage))]
[JsonSerializable(typeof(ResourceDefault))]
[JsonSerializable(typeof(NavigationBarLanguage))]
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
[JsonSerializable(typeof(ZhHans))]
[JsonSerializable(typeof(JsonElement))]
public partial class GameLauncherSourceContext : JsonSerializerContext { }

public class GameLauncherSource
{
    [JsonPropertyName("chunkDownloadSwitch")]
    public int ChunkDownloadSwitch { get; set; }

    [JsonPropertyName("keyFileCheckSwitch")]
    public int KeyFileCheckSwitch { get; set; }

    [JsonPropertyName("resourcesLogin")]
    public ResourcesLogin ResourcesLogin { get; set; }

    [JsonPropertyName("checkExeIsRunning")]
    public int CheckExeIsRunning { get; set; }

    [JsonPropertyName("hashCacheCheckAccSwitch")]
    public int HashCacheCheckAccSwitch { get; set; }

    [JsonPropertyName("fingerprints")]
    public List<string> Fingerprints { get; set; }

    [JsonPropertyName("default")]
    public ResourceDefault ResourceDefault { get; set; }

    [JsonPropertyName("RHIOptionSwitch")]
    public int RHIOptionSwitch { get; set; }

    [JsonPropertyName("predownloadSwitch")]
    public int PredownloadSwitch { get; set; }

    [JsonPropertyName("RHIOptionList")]
    public List<RHIOptionList> RHIOptionList { get; set; }

    [JsonPropertyName("experiment")]
    public Experiment Experiment { get; set; }

    [JsonPropertyName("predownload")]
    public Predownload Predownload { get; set; }

    [JsonPropertyName("keyFileCheckList")]
    public List<string> KeyFileCheckList { get; set; }
}

public class AnimateBackground
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("md5")]
    public string Md5 { get; set; }

    [JsonPropertyName("frameRate")]
    public int FrameRate { get; set; }

    [JsonPropertyName("durationInSecond")]
    public int DurationInSecond { get; set; }
}

public class AnimateBackgroundLanguage
{
    [JsonPropertyName("zh-Hans")]
    public ZhHans ZhHans { get; set; }
}

public class ResourceDefault
{
    [JsonPropertyName("sampleHashSwitch")]
    public int SampleHashSwitch { get; set; }

    [JsonPropertyName("cdnList")]
    public List<CdnList> CdnList { get; set; }

    [JsonPropertyName("resourcesBasePath")]
    public string ResourcesBasePath { get; set; }

    [JsonPropertyName("changelog")]
    public Changelog Changelog { get; set; }

    [JsonPropertyName("resources")]
    public string Resources { get; set; }

    [JsonPropertyName("resourcesExcludePathNeedUpdate")]
    public List<object> ResourcesExcludePathNeedUpdate { get; set; }

    [JsonPropertyName("config")]
    public Config Config { get; set; }

    [JsonPropertyName("resourcesDiff")]
    public ResourcesDiff ResourcesDiff { get; set; }

    [JsonPropertyName("resourcesExcludePath")]
    public List<object> ResourcesExcludePath { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("changelogVisible")]
    public int ChangelogVisible { get; set; }
}

public class Config
{
    [JsonPropertyName("indexFileMd5")]
    public string IndexFileMd5 { get; set; }

    [JsonPropertyName("unCompressSize")]
    public long UnCompressSize { get; set; }

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("patchType")]
    public string PatchType { get; set; }

    [JsonPropertyName("indexFile")]
    public string IndexFile { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("patchConfig")]
    public List<PatchConfig> PatchConfig { get; set; }
}

public class PatchConfig
{
    [JsonPropertyName("indexFileMd5")]
    public string IndexFileMd5 { get; set; }

    [JsonPropertyName("unCompressSize")]
    public long UnCompressSize { get; set; }

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("indexFile")]
    public string IndexFile { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("ext")]
    public Ext Ext { get; set; }
}

public class Ext
{
    [JsonPropertyName("maxFileSize")]
    public object MaxFileSize { get; set; }
}

public class NavigationBarLanguage
{
    [JsonPropertyName("zh-Hans")]
    public string ZhHans { get; set; }

    [JsonPropertyName("en")]
    public string En { get; set; }

    [JsonPropertyName("ja")]
    public string Ja { get; set; }

    [JsonPropertyName("ko")]
    public string Ko { get; set; }

    [JsonPropertyName("zh-Hant")]
    public string ZhHant { get; set; }

    [JsonPropertyName("fr")]
    public string Fr { get; set; }

    [JsonPropertyName("de")]
    public string De { get; set; }

    [JsonPropertyName("es")]
    public string Es { get; set; }

    [JsonPropertyName("ru")]
    public string Ru { get; set; }

    [JsonPropertyName("pt")]
    public string Pt { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("vi")]
    public string Vi { get; set; }

    [JsonPropertyName("th")]
    public string Th { get; set; }
}

public class ZhHans
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("md5")]
    public string Md5 { get; set; }

    [JsonPropertyName("frameRate")]
    public int FrameRate { get; set; }

    [JsonPropertyName("durationInSecond")]
    public int DurationInSecond { get; set; }
}
