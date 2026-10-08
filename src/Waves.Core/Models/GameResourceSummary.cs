namespace Waves.Core.Models;

public enum GameResourceAvailability { Ready, AlreadyCurrent, MissingPatch, NoPredownload }

/// <summary>配置级摘要；获取摘要不请求文件索引。</summary>
public sealed class GameResourceSummary
{
    public string? BundleName { get; init; }
    public IList<string> AvailableBundles { get; init; } = [];
    /// <summary>与更新判定使用同一来源的本地版本；分包为组合主包版本。</summary>
    public string LocalVersion { get; init; } = string.Empty;
    public string OfficialVersion { get; init; } = string.Empty;
    public string? PredownloadVersion { get; init; }
    public IList<string> HistoricalVersions { get; init; } = [];
    public bool PredownloadEnabled { get; init; }
    public GameResourceSize Install { get; init; } = new();
    public GameResourceSize Update { get; init; } = new();
    public GameResourceSize Predownload { get; init; } = new();
}

public sealed class GameResourceSize
{
    public GameResourceAvailability Availability { get; init; }
    public long DownloadSize { get; init; }
    public long TargetSize { get; init; }
    public long RequiredSpace { get; init; }
}
