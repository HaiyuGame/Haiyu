namespace Waves.Core.Models;

/// <summary>
/// Patch,Resource,Zip
/// </summary>
public class GameVersionInfo
{
    public string? BundleName { get; set; }
    public IList<GameResourcePackInfo> ResourcePacks { get; set; } = [];
    public GameResourceAvailability Availability { get; set; } = GameResourceAvailability.Ready;
    public IList<GameResourceCdn> CdnCandidates { get; set; } = [];
    public long DownloadSize { get; set; }
    public long TargetSize { get; set; }
    public long RequiredSpace { get; set; }
    /// <summary>
    /// 资源版本，分包版本为2，默认版本为1
    /// </summary>
    public string ResourceVersion { get; set; } = string.Empty;

    /// <summary>
    /// 旧游戏版本
    /// </summary>
    public string OldGameVersion { get; set; } = string.Empty;

    /// <summary>
    /// 游戏版本
    /// </summary>
    public string NewGameVersion { get; set; } = string.Empty;

    /// <summary>
    /// 压缩包资源
    /// </summary>
    public IList<PatchGameFileInfo> ZipResources { get; set; } = [];

    /// <summary>
    /// 补丁包
    /// </summary>
    public IList<PatchGameFileInfo> PatchResources { get; set; } = [];

    /// <summary>
    /// 本地直接资源
    /// </summary>
    public IList<GameFileInfo> DefaultResource { get; set; } = [];

    public IList<string> DeleteFiles { get; set; } = [];


}

/// <summary>
/// 单文件信息
/// </summary>
public class GameFileInfo
{
    /// <summary>分包来源；Dest 始终是游戏内相对路径，不含远端 folder。</summary>
    public string? ResourcePackName { get; set; }
    /// <summary>分包下载载体的缓存相对路径；独立于安装路径。</summary>
    public string? CacheRelativePath { get; set; }
    /// <summary>保留备选 Apply 描述，但执行器仅消费已选择的载体。</summary>
    public bool IsSelected { get; set; } = true;
    public IList<string> UrlCandidates { get; set; } = [];
    public string Url { get; set; } = string.Empty;

    public string? FromFolder { get; set; }

    public string Dest { get; set; } = string.Empty;

    public long Size { get; set; }


    public long? Start { get; set; }

    public long? End { get; set; }

    public string Hash { get; set; } = string.Empty;

    public IList<GameFileChunkInfo> Chunks { get; set; } = [];


    /// <summary>
    /// 是否分片
    /// </summary>
    public bool IsChunk => Chunks.Count != 0;
}

/// <summary>
/// 
/// </summary>
public class PatchGameFileInfo : GameFileInfo
{
    public bool IsGroup { get; set; }
    /// <summary>
    /// ZIP/Patch 索引中的 entries。
    /// </summary>
    public IList<GameFileInfo> Entries { get; set; } = [];

    /// <summary>
    /// Group Apply 使用的源文件。
    /// </summary>
    public IList<GameFileInfo> SrcFiles { get; set; } = [];

    /// <summary>
    /// Group Apply 产生的目标文件。
    /// </summary>
    public IList<GameFileInfo> DstFiles { get; set; } = [];
}

/// <summary>保留 CDN 测速权重，不携带服务端配置 DTO。</summary>
public sealed class GameResourceCdn
{
    public string Url { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int SpeedWeight { get; set; } = 1;
    public int PriorityWeight { get; set; }
}

/// <summary>协议无关的闭区间分片校验信息。</summary>
public sealed class GameFileChunkInfo
{
    public long Start { get; set; }
    public long End { get; set; }
    public string Hash { get; set; } = string.Empty;
}

public enum GameResourcePackKind { Origin, Zip, Patch }

/// <summary>逐包版本和索引身份，供后续执行器写入版本和隔离缓存使用。</summary>
public sealed class GameResourcePackInfo
{
    public string Name { get; set; } = string.Empty;
    public string LocalVersion { get; set; } = string.Empty;
    public string TargetVersion { get; set; } = string.Empty;
    public GameResourceAvailability Availability { get; set; }
    public GameResourcePackKind Kind { get; set; }
    public IList<string> IndexUrlCandidates { get; set; } = [];
    public string IndexHash { get; set; } = string.Empty;
    public long DownloadSize { get; set; }
    public long TargetSize { get; set; }
    public long RequiredSpace { get; set; }
    public long DeltaSize { get; set; }
    public long MaxFileSize { get; set; }
}

[JsonSerializable(typeof(GameVersionInfo))]
[JsonSerializable(typeof(GameResourceSummary))]
public partial class GameResourceJsonContext : JsonSerializerContext { }
