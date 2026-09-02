using System.Runtime.InteropServices;
using ABI.Models;
using ABIRuntime.Abstractions;
using Haiyu.ABI.Common;
using Haiyu.ABI.Common.Navtive;
using NativeMemory = Haiyu.ABI.Common.NativeMemory;

namespace Haiyu.ABI.Services;

/// <summary>
/// 清理内存接口
/// </summary>
public class MemoryCleanerService
    : IPrivilegedService<CleanMemoryRequest, RunResult, CleanMemoryProgress>
{
    public PrivilegedServiceContract<CleanMemoryRequest, RunResult, CleanMemoryProgress> Contract =>
        ABIRuntime.Contract.CleanMemoryContract;

    public async ValueTask<RunResult> ExecuteAsync(
        CleanMemoryRequest request,
        IProgress<CleanMemoryProgress> progress,
        CancellationToken cancellationToken
    )
    {
        return await Task.FromResult(new RunResult(0, "测试成功"));
    }

    /// <summary>
    /// 清理待机内存，释放可直接重新利用的缓存页面
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void PurgeStandbyList()
    {
        var status = NativeMemory.ExecuteMemoryCommand(
            NativeMemory.SystemMemoryListCommand.MemoryPurgeStandbyList
        );

        if (!NativeMemory.NtSuccess(status))
        {
            throw new InvalidOperationException(
                $"Purge standby list failed. NTSTATUS: 0x{status:X8}"
            );
        }
    }

    /// <summary>
    /// 清理低优先级待机内存，相比完整待机清理更加温和
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void PurgeLowPriorityStandbyList()
    {
        var status = NativeMemory.ExecuteMemoryCommand(
            NativeMemory.SystemMemoryListCommand.MemoryPurgeLowPriorityStandbyList
        );

        if (!NativeMemory.NtSuccess(status))
        {
            throw new InvalidOperationException(
                $"Purge low priority standby list failed. NTSTATUS: 0x{status:X8}"
            );
        }
    }

    /// <summary>
    /// 将已修改内存页写回后备存储，使其可以被系统回收
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void FlushModifiedPageList()
    {
        var status = NativeMemory.ExecuteMemoryCommand(
            NativeMemory.SystemMemoryListCommand.MemoryFlushModifiedList
        );

        if (!NativeMemory.NtSuccess(status))
        {
            throw new InvalidOperationException(
                $"Flush modified page list failed. NTSTATUS: 0x{status:X8}"
            );
        }
    }

    /// <summary>
    /// 收缩进程工作集，释放部分当前驻留的物理内存
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void EmptyWorkingSets()
    {
        var status = NativeMemory.ExecuteMemoryCommand(
            NativeMemory.SystemMemoryListCommand.MemoryEmptyWorkingSets
        );

        if (!NativeMemory.NtSuccess(status))
        {
            throw new InvalidOperationException(
                $"Empty working sets failed. NTSTATUS: 0x{status:X8}"
            );
        }
    }

    /// <summary>
    /// 清理系统文件缓存，释放 Windows 文件系统缓存占用的部分物理内存
    /// 权限调用：NativePrivilege.EnablePrivilege("SeIncreaseQuotaPrivilege");
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void PurgeSystemFileCache()
    {
        var flushValue = unchecked((nuint)(-1));

        if (!NativeMemory.SetSystemFileCacheSize(flushValue, flushValue, 0))
        {
            throw new InvalidOperationException(
                $"Purge system file cache failed. Win32Error: {Marshal.GetLastWin32Error()}"
            );
        }
    }

    /// <summary>
    /// 合并物理内存中的重复页面，减少重复页面占用的物理内存
    /// 调用：NativePrivilege.EnablePrivilege("SeProfileSingleProcessPrivilege");
    /// </summary>
    /// <returns>成功合并的页面数量</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public nuint CombinePhysicalMemoryPages()
    {
        var information = new NativeMemory.MemoryCombineInformation
        {
            Handle = 0,
            PagesCombined = 0,
            Flags = 0,
        };

        var status = NativeMemory.NtSetSystemInformation(
            NativeMemory.SystemCombinePhysicalMemoryInformation,
            ref information,
            (uint)Marshal.SizeOf<NativeMemory.MemoryCombineInformation>()
        );

        if (!NativeMemory.NtSuccess(status))
        {
            throw new InvalidOperationException(
                $"Combine physical memory pages failed. NTSTATUS: 0x{status:X8}"
            );
        }

        return information.PagesCombined;
    }

    /// <summary>
    /// 刷新磁盘缓存，传入磁盘盘符（如 C、D、E 等），将已修改的文件缓存写回磁盘，释放占用的内存
    /// </summary>
    /// <param name="driveLetter"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void FlushModifiedFileCache(char driveLetter)
    {
        var volumePath = $@"\\.\{char.ToUpperInvariant(driveLetter)}:";

        var handle = NativeFileCache.CreateFile(
            volumePath,
            NativeFileCache.GENERIC_READ | NativeFileCache.GENERIC_WRITE,
            NativeFileCache.FILE_SHARE_READ | NativeFileCache.FILE_SHARE_WRITE,
            0,
            NativeFileCache.OPEN_EXISTING,
            0,
            0
        );

        if (handle == NativeFileCache.INVALID_HANDLE_VALUE)
        {
            throw new InvalidOperationException(
                $"Open volume {volumePath} failed. Win32Error: {Marshal.GetLastWin32Error()}"
            );
        }

        try
        {
            if (!NativeFileCache.FlushFileBuffers(handle))
            {
                throw new InvalidOperationException(
                    $"Flush volume {volumePath} failed. Win32Error: {Marshal.GetLastWin32Error()}"
                );
            }
        }
        finally
        {
            NativeFileCache.CloseHandle(handle);
        }
    }

    /// <summary>
    /// 刷新注册表缓存，将注册表 Hive 的待处理数据进行协调和写回
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    internal static void ExecuteRegistryReconciliation()
    {
        var status = NativeMemory.ExecuteRegistryReconciliation();

        if (!NativeMemory.NtSuccess(status))
        {
            throw new InvalidOperationException(
                $"Flush registry cache failed. NTSTATUS: 0x{status:X8}"
            );
        }
    }
}
