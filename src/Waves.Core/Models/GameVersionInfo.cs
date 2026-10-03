namespace Waves.Core.Models;

/// <summary>
/// Patch,Resource,Zip
/// </summary>
public class GameVersionInfo
{
    /// <summary>
    /// 资源版本，分包版本为2，默认版本为1
    /// </summary>
    public string ResourceVersion { get; set; } = string.Empty;

    /// <summary>
    /// 旧游戏版本
    /// </summary>
    public string OldGameVersion { get; set; }

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
    public string Dest { get; set; } = string.Empty;

    public long Size { get; set; }


    public long? Start { get; set; }

    public long? End { get; set; }

    public string Hash { get; set; } = string.Empty;

    public IList<IndexChunkInfo> Chunks { get; set; } = [];


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
