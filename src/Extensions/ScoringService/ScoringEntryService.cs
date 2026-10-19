namespace ScoringService;

/// <summary>词条存储服务入口。JSON 增删改查将在计算核心验证后接入。</summary>
public sealed class ScoringEntryService
{
    /// <summary>词条与模板文件的本地存储根目录，已转换为绝对路径。</summary>
    public string BaseFolder { get; }

    public ScoringEntryService(string baseFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseFolder);
        BaseFolder = Path.GetFullPath(baseFolder);
    }
}
