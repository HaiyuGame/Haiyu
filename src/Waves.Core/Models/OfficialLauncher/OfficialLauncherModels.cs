namespace Waves.Core.Models.OfficialLauncher;

/// <summary>
/// 官方启动器（KRLauncher）游戏目录安装配置：launcherDownloadConfig.json。
/// 字段与官方 KRInstalledConfig/BundleInstalledInfo 的 JsonPropertyName 严格对齐，
/// 用于官方启动器"定位游戏目录"时识别已安装状态，避免全量重下。
/// </summary>
public sealed class OfficialInstalledConfig
{
    /// <summary>对应官方 appId 字段，取值 = KuroGameApiConfig.AppId（即 resId，如 10003）。</summary>
    [JsonPropertyName("appId")]
    public string AppId { get; set; } = string.Empty;

    /// <summary>顶层版本：旧协议（无 bundles）的安装状态；新协议下仅作 default-bundle 兼容视图。</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>顶层状态：旧协议瞬态标记；新协议下为空串。</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>键 = bundle 名（服务器原样拼写，如 HD/SD/UHD）。</summary>
    [JsonPropertyName("bundles")]
    public Dictionary<string, OfficialBundleInstalledInfo> Bundles { get; set; } = new();
}

/// <summary>官方 BundleInstalledInfo 对应结构。</summary>
public sealed class OfficialBundleInstalledInfo
{
    /// <summary>该 bundle 已安装版本（= 主分包版本）。</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>官方瞬态标记（moving/repairing），写入时为空串。</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>已安装分包名列表，必须列全服务器该 bundle 的全部分包（服务器原样拼写）。</summary>
    [JsonPropertyName("resourcePacks")]
    public List<string> ResourcePacks { get; set; } = [];
}

/// <summary>
/// 官方分包版本文件：launcherDownloadConfig/&lt;pack&gt;.json（PackVersionConfig 对应结构）。
/// 该版本号是官方后续 patch 增量更新的基线，必须与服务器分包版本精确相等。
/// </summary>
public sealed class OfficialPackVersionConfig
{
    [JsonPropertyName("packName")]
    public string PackName { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// 官方本地资源清单：LocalGameResources.json（KRGameResourceRecord 对应结构）。
/// 官方所有读取点均为 best-effort 簿记，缺失不触发下载；写入后对官方修复场景有益。
/// </summary>
public sealed class OfficialGameResourceRecord
{
    [JsonPropertyName("resource")]
    public List<OfficialResourceFileInfo> Resource { get; set; } = [];
}

/// <summary>官方 KRFileInfo 对应结构；路径字段名是 dest（不是 path）。</summary>
public sealed class OfficialResourceFileInfo
{
    /// <summary>相对游戏目录的路径，无前导 /。</summary>
    [JsonPropertyName("dest")]
    public string Dest { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("md5")]
    public string Md5 { get; set; } = string.Empty;

    [JsonPropertyName("fromFolder")]
    public string? FromFolder { get; set; }

    /// <summary>与官方 {start,end,md5} 对应；无分块时保持 null（官方大文件才有）。</summary>
    [JsonPropertyName("chunkInfos")]
    public List<OfficialResourceChunkInfo>? ChunkInfos { get; set; }
}

/// <summary>官方分块校验信息。</summary>
public sealed class OfficialResourceChunkInfo
{
    [JsonPropertyName("start")]
    public long Start { get; set; }

    [JsonPropertyName("end")]
    public long End { get; set; }

    [JsonPropertyName("md5")]
    public string Md5 { get; set; } = string.Empty;
}

/// <summary>
/// 待写入的官方安装记录：按画质组合（bundle）分组，每组含 appId、bundle 名与逐分包版本。
/// 多个画质组合共用一个游戏目录（官方 bundles 字典天然支持多节点，如 HD 与 UHD 并存）。
/// </summary>
public sealed record OfficialInstallRecord(
    string AppId,
    string BundleName,
    IReadOnlyList<OfficialPackVersion> Packs
)
{
    /// <summary>旧协议游戏：官方格式为顶层 version/state（无 bundles/分包版本文件）。</summary>
    public bool IsLegacy { get; init; }

    /// <summary>同目录已安装的其他画质组合（不含主 BundleName），写入时一并登记 bundle 节点。</summary>
    public IReadOnlyList<OfficialBundleRecord> AdditionalBundles { get; init; } = [];
}

/// <summary>单个画质组合的安装记录（用于多画质并存时的附加 bundle 节点）。</summary>
public sealed record OfficialBundleRecord(
    string BundleName,
    IReadOnlyList<OfficialPackVersion> Packs
);

/// <summary>单个分包的目标版本。</summary>
public sealed record OfficialPackVersion(string Name, string Version);

[JsonSerializable(typeof(OfficialInstalledConfig))]
[JsonSerializable(typeof(OfficialPackVersionConfig))]
[JsonSerializable(typeof(OfficialGameResourceRecord))]
public partial class OfficialLauncherJsonContext : JsonSerializerContext
{
}
