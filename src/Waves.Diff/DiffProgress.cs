namespace Waves.Diff;

/// <summary>
/// 进度快照，六字段与 hpatchz 共享内存协议（Haiyu KrDiffDecompressResult）一一对应：
/// [0]已完成文件数 [1]文件总数 [2]当前文件已写字节 [3]当前文件总大小 [4]全局已写字节 [5]总字节数。
/// </summary>
public readonly struct DiffProgress
{
    /// <summary>Completed is reported only after validation, flushing and output publication succeed.</summary>
    public DiffPatchStage Stage { get; init; }
    public bool IsCompleted => Stage == DiffPatchStage.Completed;
    public required ulong FileTotalCount { get; init; }
    public required ulong PatchedFileCount { get; init; }
    public required ulong PatchingFileCurrentSize { get; init; }
    public required ulong PatchingFileTotalSize { get; init; }
    public required ulong PatchedCurrentBytes { get; init; }
    public required ulong PatchTotalBytes { get; init; }

    /// <summary>
    /// Waves.Diff 内部按实际处理输出字节增量计算的速度，单位为字节/秒。
    /// 该值不依赖系统进程 I/O 计数器；开始阶段可能为 0。
    /// </summary>
    public double SpeedValue { get; init; }
}
