using System;
using System.Collections.Generic;
using System.Text;

namespace Waves.Core.Models;

public struct KrDiffDecompressResult
{
    /// <summary>
    /// 已完成补丁的文件数量
    /// </summary>
    public ulong PatchedFileCount;

    public KrDiffDecompressResult(
        ulong patchedFileCount,
        ulong fileTotalCount,
        ulong patchingFileCurrentSize,
        ulong patchingFileTotalSize,
        ulong patchedCurrentBytes,
        ulong patchTotalBytes
    )
        : this()
    {
        PatchedFileCount = patchedFileCount;
        FileTotalCount = fileTotalCount;
        PatchingFileCurrentSize = patchingFileCurrentSize;
        PatchingFileTotalSize = patchingFileTotalSize;
        PatchedCurrentBytes = patchedCurrentBytes;
        PatchTotalBytes = patchTotalBytes;
    }

    /// <summary>
    /// 本次需要补丁的总文件数
    /// </summary>
    public ulong FileTotalCount;

    /// <summary>
    /// 当前正在补丁的文件 - 已处理大小
    /// </summary>
    public ulong PatchingFileCurrentSize;

    /// <summary>
    /// 当前正在补丁的文件 - 总大小
    /// </summary>
    public ulong PatchingFileTotalSize;

    /// <summary>
    /// 整体补丁 - 已处理总字节数
    /// </summary>
    public ulong PatchedCurrentBytes;

    /// <summary>
    /// 整体补丁 - 总字节数
    /// </summary>
    public ulong PatchTotalBytes;

    #region 可选：计算属性（直接拿百分比，界面直接用）
    /// <summary>
    /// 文件总进度百分比 (0~100)
    /// </summary>
    public double FileProgress =>
        FileTotalCount == 0 ? 0 : (double)PatchedFileCount / FileTotalCount * 100;

    /// <summary>
    /// 当前文件处理进度百分比 (0~100)
    /// </summary>
    public double CurrentFileProgress =>
        PatchingFileTotalSize == 0
            ? 0
            : (double)PatchingFileCurrentSize / PatchingFileTotalSize * 100;

    /// <summary>
    /// 整体字节总进度百分比 (0~100)
    /// </summary>
    public double TotalBytesProgress =>
        PatchTotalBytes == 0 ? 0 : (double)PatchedCurrentBytes / PatchTotalBytes * 100;

    public double SpeedValue { get; internal set; }
    #endregion
}
