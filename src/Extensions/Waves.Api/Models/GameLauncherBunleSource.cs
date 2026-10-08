using System.Text.Json.Serialization;

namespace Waves.Api.Models;


[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(GameLauncherBunleSource))]
[JsonSerializable(typeof(Dictionary<string, BundleResourcePack>))]
[JsonSerializable(typeof(Dictionary<string, GameLauncherBundle>))]
[JsonSerializable(typeof(List<BundlePatchConfig>))]
[JsonSerializable(typeof(BundlePredownload))]
public partial class GameLauncherBunleSourceContext : JsonSerializerContext;

public sealed class GameLauncherBunleSource
{
    [JsonPropertyName("cdnList")]
    public List<BundleCdnSource> CdnList { get; set; } = [];

    [JsonPropertyName("resourcePacks")]
    public Dictionary<string, BundleResourcePack> ResourcePacks { get; set; } = [];

    [JsonPropertyName("bundles")]
    public Dictionary<string, GameLauncherBundle> Bundles { get; set; } = [];

    [JsonPropertyName("config")]
    public BundleLauncherConfig Config { get; set; } = new();

    [JsonPropertyName("resourcesGray")]
    public BundleResourcesGray? ResourcesGray { get; set; }

    [JsonPropertyName("predownload")]
    public BundlePredownload? Predownload { get; set; }
}

public sealed class BundleCdnSource
{
    [JsonPropertyName("K1")]
    public int K1 { get; set; }

    [JsonPropertyName("K2")]
    public int K2 { get; set; }

    [JsonPropertyName("P")]
    public int P { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public sealed class BundleResourcePack
{
    [JsonPropertyName("folder")]
    public string Folder { get; set; } = string.Empty;

    [JsonPropertyName("zipConfig")]
    public BundlePatchConfig? ZipConfig { get; set; }
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("indexFile")]
    public string IndexFile { get; set; } = string.Empty;

    [JsonPropertyName("indexFileMd5")]
    public string IndexFileMd5 { get; set; } = string.Empty;

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("unCompressSize")]
    public long UnCompressSize { get; set; }

    [JsonPropertyName("patchType")]
    public string PatchType { get; set; } = string.Empty;

    [JsonPropertyName("patchConfig")]
    public List<BundlePatchConfig> PatchConfig { get; set; } = [];
}

public sealed class BundlePatchConfig
{
    [JsonPropertyName("folder")]
    public string Folder { get; set; } = string.Empty;
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("indexFile")]
    public string IndexFile { get; set; } = string.Empty;

    [JsonPropertyName("indexFileMd5")]
    public string IndexFileMd5 { get; set; } = string.Empty;

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("unCompressSize")]
    public long UnCompressSize { get; set; }

    [JsonPropertyName("ext")]
    public BundlePatchExtension Ext { get; set; } = new();
}

public sealed class BundlePatchExtension
{
    [JsonPropertyName("requiredDiskSpace")]
    public long? RequiredDiskSpace { get; set; }

    [JsonPropertyName("deltaSize")]
    public long? DeltaSize { get; set; }

    [JsonPropertyName("maxFileSize")]
    public long? MaxFileSize { get; set; }

    [JsonPropertyName("applyEvals")]
    public List<BundleApplyEvaluation>? ApplyEvaluations { get; set; }
}

public sealed class BundleApplyEvaluation
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("requiredDiskSpace")]
    public long RequiredDiskSpace { get; set; }

    [JsonPropertyName("maxFileSize")]
    public long MaxFileSize { get; set; }

    [JsonPropertyName("deltaSize")]
    public long DeltaSize { get; set; }
}

public sealed class GameLauncherBundle
{
    [JsonPropertyName("resourcePacks")]
    public List<string> ResourcePacks { get; set; } = [];

    [JsonPropertyName("config")]
    public BundleProfileConfig Config { get; set; } = new();
}

public sealed class BundleProfileConfig
{
    [JsonPropertyName("predownloadSwitch")]
    public int? PredownloadSwitch { get; set; }

    [JsonPropertyName("displayName")]
    public Dictionary<string, string> DisplayName { get; set; } = [];

    [JsonPropertyName("recommendGpu")]
    public List<string> RecommendGpu { get; set; } = [];

    [JsonPropertyName("experiment")]
    public BundleExperiment Experiment { get; set; } = new();

    [JsonPropertyName("RHIOptionSwitch")]
    public int RhiOptionSwitch { get; set; }

    [JsonPropertyName("RHIOptionList")]
    public List<BundleRhiOption> RhiOptionList { get; set; } = [];

    [JsonPropertyName("commandSwitch")]
    public int CommandSwitch { get; set; }

    [JsonPropertyName("commandList")]
    public List<BundleCommand> CommandList { get; set; } = [];
}

public sealed class BundleLauncherConfig
{
    [JsonPropertyName("keyFileCheckSwitch")]
    public int KeyFileCheckSwitch { get; set; }

    [JsonPropertyName("keyFileCheckList")]
    public List<string> KeyFileCheckList { get; set; } = [];

    [JsonPropertyName("predownloadSwitch")]
    public int PredownloadSwitch { get; set; }

    [JsonPropertyName("experiment")]
    public BundleExperiment Experiment { get; set; } = new();

    [JsonPropertyName("fingerprints")]
    public List<string> Fingerprints { get; set; } = [];

    [JsonPropertyName("commandSwitch")]
    public int CommandSwitch { get; set; }

    [JsonPropertyName("commandList")]
    public List<BundleCommand> CommandList { get; set; } = [];

    [JsonPropertyName("RHIOptionSwitch")]
    public int RhiOptionSwitch { get; set; }

    [JsonPropertyName("RHIOptionList")]
    public List<BundleRhiOption> RhiOptionList { get; set; } = [];

    [JsonPropertyName("resourceLevelHintSwitch")]
    public int ResourceLevelHintSwitch { get; set; }

    [JsonPropertyName("resourceLevelHint")]
    public Dictionary<string, string> ResourceLevelHint { get; set; } = [];

    [JsonPropertyName("functionCode")]
    public BundleFunctionCode FunctionCode { get; set; } = new();
}

public sealed class BundleExperiment
{
    [JsonPropertyName("res_check")]
    public BundleResourceCheckExperiment ResourceCheck { get; set; } = new();

    [JsonPropertyName("download")]
    public BundleDownloadExperiment Download { get; set; } = new();

    [JsonPropertyName("apply")]
    public BundleApplyExperiment Apply { get; set; } = new();

    [JsonPropertyName("repair")]
    public BundleRepairExperiment Repair { get; set; } = new();

    [JsonPropertyName("mainButton")]
    public BundleMainButtonExperiment MainButton { get; set; } = new();
}

public sealed class BundleResourceCheckExperiment
{
    [JsonPropertyName("fileSizeCheckSwitch")]
    public string FileSizeCheckSwitch { get; set; } = string.Empty;

    [JsonPropertyName("fileChunkCheckSwitch")]
    public string FileChunkCheckSwitch { get; set; } = string.Empty;

    [JsonPropertyName("resValidCheckTimeOut")]
    public string ResourceValidCheckTimeout { get; set; } = string.Empty;

    [JsonPropertyName("fileCheckWhiteListConfig")]
    public string FileCheckWhiteListConfig { get; set; } = string.Empty;
}

public sealed class BundleDownloadExperiment
{
    [JsonPropertyName("downloadReadBlockTimeout")]
    public string DownloadReadBlockTimeout { get; set; } = string.Empty;

    [JsonPropertyName("downloadCdnSelectTestDuration")]
    public string DownloadCdnSelectTestDuration { get; set; } = string.Empty;
}

public sealed class BundleApplyExperiment
{
    [JsonPropertyName("applyMethodFeature")]
    public string ApplyMethodFeature { get; set; } = string.Empty;
}

public sealed class BundleRepairExperiment
{
    [JsonPropertyName("directoryIntegrityCheckList")]
    public string DirectoryIntegrityCheckList { get; set; } = string.Empty;
}

public sealed class BundleMainButtonExperiment
{
    [JsonPropertyName("exitGameEntry")]
    public string ExitGameEntry { get; set; } = string.Empty;
}

public sealed class BundleRhiOption
{
    [JsonPropertyName("cmdOption")]
    public string CommandOption { get; set; } = string.Empty;

    [JsonPropertyName("isShow")]
    public int IsShow { get; set; }

    [JsonPropertyName("text")]
    public Dictionary<string, string> Text { get; set; } = [];
}

public sealed class BundleCommand
{
    [JsonPropertyName("default")]
    public int Default { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("cmd")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public Dictionary<string, string> Text { get; set; } = [];
}

public sealed class BundleFunctionCode
{
    [JsonPropertyName("background")]
    public string Background { get; set; } = string.Empty;
}

// 当前协议样本中为 null；保留独立强类型扩展点。
public sealed class BundleResourcesGray;

/// <summary>
/// 预下载版本配置。结构与正式版本的资源部分一致，但不包含启动器级配置。
/// </summary>
public sealed class BundlePredownload
{
    [JsonPropertyName("cdnList")]
    public List<BundleCdnSource> CdnList { get; set; } = [];

    [JsonPropertyName("resourcePacks")]
    public Dictionary<string, BundleResourcePack> ResourcePacks { get; set; } = [];

    [JsonPropertyName("bundles")]
    public Dictionary<string, GameLauncherBundle> Bundles { get; set; } = [];
}
