namespace Waves.Core.Models;

/// <summary>官方启动器安装记录定位结果状态。</summary>
public enum OfficialRecordLocateState
{
    /// <summary>非鸣潮游戏：本工具仅适配鸣潮官方启动器的目录格式</summary>
    NotWavesGame,

    /// <summary>游戏目录未设置或不存在</summary>
    GameFolderMissing,

    /// <summary>游戏目录为空目录</summary>
    GameFolderEmpty,

    /// <summary>游戏目录中找不到游戏主程序（游戏可能未安装完成）</summary>
    GameExeMissing,

    /// <summary>定位成功</summary>
    Ready,
}

/// <summary>官方启动器安装记录定位结果。</summary>
public class OfficialRecordLocateResult
{
    public OfficialRecordLocateState State { get; init; }

    /// <summary>游戏目录完整路径，仅 Ready 状态有效</summary>
    public string GameFolder { get; init; } = string.Empty;

    /// <summary>游戏上下文名（如 WavesMainGameContextV2），仅 Ready 状态有效</summary>
    public string ContextName { get; init; } = string.Empty;

    /// <summary>该目录当前是否已存在官方安装配置（launcherDownloadConfig.json）</summary>
    public bool HasExistingRecord { get; init; }

    /// <summary>目录中已有的 bundle 名列表（仅 Ready 且 HasExistingRecord 时有效）</summary>
    public IReadOnlyList<string> ExistingBundles { get; init; } = [];
}

/// <summary>官方记录写入结果汇总。</summary>
public class OfficialRecordWriteResult
{
    /// <summary>成功写入（或幂等跳过）的文件相对路径</summary>
    public IReadOnlyList<string> WrittenFiles { get; init; } = [];

    /// <summary>写入失败的文件相对路径（失败详情见应用日志）</summary>
    public IReadOnlyList<string> FailedFiles { get; init; } = [];

    /// <summary>被删除的文件/目录相对路径</summary>
    public IReadOnlyList<string> RemovedEntries { get; init; } = [];

    public bool HasFailed => FailedFiles.Count > 0;

    public int WrittenCount => WrittenFiles.Count;
}
