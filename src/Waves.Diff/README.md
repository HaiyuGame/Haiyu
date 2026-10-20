# Waves.Diff

为 Haiyu 提供 HDIFF19 目录重建，以及 HDIFF13 / HDIFFSF20 单文件重建。支持 raw/zstd、同步/异步 I/O、取消、进度与 NativeAOT。

## 目录更新

```csharp
await DiffPatcher.PatchDirectoryAsync(oldDirectory, patchFile, outputDirectory,
    onProgress: progress =>
    {
        // Bytes reaching the total does not imply validation/publication has finished.
        if (progress.IsCompleted) { /* validated and published */ }
    }, cancellationToken: cancellationToken);
```

原有五参数同步/异步入口保留。目录内嵌 HDIFF13 与 HDIFFSF20 自动识别。

- 默认全局校验为 New + Copy，与 Haiyu 的原生参数一致；不额外扫描旧引用及整个补丁。
- 固定 fadler64 逐文件检查随 New 选择启用。库将其不匹配视为错误，而非只打印通知。
- 全局校验类型支持 fadler64 / CRC32 / MD5，以及没有保存全局校验的目录头。
- 当逐文件和整体新数据校验都为 fadler64 时，复用已验证的逐文件校验状态合并整体结果，避免重复扫描输出数据；校验项保持不变。
- 原地更新先重建到唯一的旁路暂存目录，重建或校验失败、提交前取消时丢弃暂存输出，不提前发布新文件。
- 校验后只发布补丁生成的路径；不会清理 OldPaths、SrcFiles、补丁包或其他旧文件。这些清理由 Haiyu 的安装逻辑负责。
- 提交是一系列文件操作，不是整个目录的原子事务；提交开始后不响应取消。提交中的磁盘/权限异常仍可能留下部分已发布结果。
- 输出目录不同于旧目录时直接写入输出目录，适配 Haiyu 已有的临时目录与后续搬运流程。

严格校验可显式选择：

```csharp
await DiffPatcher.PatchDirectoryAsync(oldDirectory, patchFile, outputDirectory,
    DiffPatchOptions.Strict, onProgress, cancellationToken);

var options = new DiffPatchOptions
{
    Checksums = DiffChecksumValidation.New | DiffChecksumValidation.Copy
};
```

选项为不可变值类型，默认初始化值也为 New + Copy；显式 None 关闭校验选择。尺寸、路径、越界、流耗尽等格式校验仍然执行。

## 文件重建

```csharp
await DiffPatcher.PatchFileAsync(oldFile, patchFile, newFile, onProgress, cancellationToken);
```

按头自动选择 HDIFF13 或 HDIFFSF20。文件输出必须与两个输入路径不同。单文件入口保持直接写出语义，不提供原地发布事务。

## 进度与取消

DiffProgress 的原有六字段保留，增加 Stage、IsCompleted 和 SpeedValue。SpeedValue 由 Waves.Diff 内部根据实际处理的输出字节增量与单调时钟计算，单位为字节/秒，不读取系统进程 I/O 计数器；开始阶段样本不足时为 0。Stage 为 Patching、Committing、Completed；只有校验、输出关闭及发布成功后发送 Completed。最终成功以同步方法正常返回或 Task 成功完成为准，不以字节百分比为准。

回调直接在执行线程/续体上调用，不自动切回 UI。异步路径使用异步文件 I/O；hash、RLE 和解压仍包含 CPU 工作。HDIFF13 的小块元数据游标仍为同步读取。UI 需要隔离全部 CPU 工作时，应在调用层统一调度。

## 内存边界

HDIFF13 的 cover/code/extra 流有界读取；重建与复制复用池化缓冲；旧引用句柄使用有界 LRU。HDIFFSF20 单步 cover/RLE 元数据在 8 MiB 内使用池，大于此阈值自动落盘并在正常结束、失败和取消后释放临时文件。目录路径/索引元数据仍随文件数增长，Zstd 原生窗口与文件系统缓存另外计算。

## 验证范围

70 项生成样本回归覆盖同步/异步、原 exe 输出对比、HDIFF13 四流组合、SF20 多步骤与目录嵌套、校验选择、原地旧源保留、阶段通知、错误与取消、大步骤磁盘暂存，以及 fadler64 状态合并和长度回绕。

真实官方 krpdiff 的 NativeAOT / 原 exe 同条件测试方法和结果见 PERFORMANCE.md；样本测试不代表所有补丁的性能。

尚未以所有真实游戏版本补丁验收；未新增 zlib/bzip2/lzma/7zXZ 等插件、裸 BSDIFF/VCDIFF、SFX 或完整 CLI。路径字符串检查也不替代对已有 symlink/junction 的文件系统隔离。

此前调用链分析记录见 HAIYU_CALL_CHAIN.md；其中对旧实现的差异描述属于历史调查，不代表本次更新后的默认行为。
