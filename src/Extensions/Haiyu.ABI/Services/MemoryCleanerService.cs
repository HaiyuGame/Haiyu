using System.Diagnostics;
using System.Runtime.InteropServices;
using ABI.Models;
using ABIRuntime.Abstractions;
using Haiyu.ABI.Common;
using Haiyu.ABI.Common.Navtive;
using Microsoft.Diagnostics.Tracing.StackSources;
using NativeMemory = Haiyu.ABI.Common.NativeMemory;

namespace Haiyu.ABI.Services;

/// <summary>
/// 清理内存接口
/// </summary>
public class MemoryCleanerService
    : IPrivilegedService<CleanMemoryRequest, CleanMemoryProgress, RunResult>
{
    public PrivilegedServiceContract<CleanMemoryRequest, CleanMemoryProgress, RunResult> Contract =>
        ABIRuntime.Contract.CleanMemoryContract;

    public ValueTask<RunResult> ExecuteAsync(
        CleanMemoryRequest request,
        IProgress<CleanMemoryProgress> progress,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);

        var operationCount = CountEnabledOperations(request);
        var completedCount = 0;

        progress.Report(new CleanMemoryProgress(0, "正在准备内存清理"));
        cancellationToken.ThrowIfCancellationRequested();

        // SystemMemoryListInformation 和物理页合并均需要该权限。高权限宿主
        // 已完成提权，但 Windows 仍要求显式启用 Token 中的对应权限。
        if (
            request.PurgeStandbyEnable
            || request.PurgeLowPriorityStandbyEnable
            || request.FlushModifiedEnable
            || request.EmptyWorkingSetsEnable
            || request.CombinePhysicalMemoryEnable
            || request.ExecuteRegistryEnable
        )
        {
            NativePrivilege.EnablePrivilege("SeProfileSingleProcessPrivilege");
        }

        if (request.PurgeSystemFileEnable)
        {
            NativePrivilege.EnablePrivilege("SeIncreaseQuotaPrivilege");
        }

        void Execute(bool enabled, Action action, string message)
        {
            if (!enabled)
                return;
            cancellationToken.ThrowIfCancellationRequested();
            action();
            completedCount++;
            progress.Report(
                new CleanMemoryProgress(GetPercentage(completedCount, operationCount), message)
            );
        }

        Execute(
            request.PurgeLowPriorityStandbyEnable,
            PurgeLowPriorityStandbyList,
            "已清理低优先级待机内存"
        );
        Execute(request.PurgeStandbyEnable, PurgeStandbyList, "已清理待机内存");
        Execute(request.FlushModifiedEnable, FlushModifiedPageList, "已写回已修改内存页");
        Execute(
            request.EmptyWorkingSetsEnable,
            () => EmptyRequestedWorkingSingleSets(request.programNames, cancellationToken),
            string.IsNullOrWhiteSpace(request.programNames)
                ? "已收缩系统进程工作集"
                : "已收缩指定进程工作集"
        );
        Execute(request.PurgeSystemFileEnable, PurgeSystemFileCache, "已清理系统文件缓存");
        Execute(
            request.CombinePhysicalMemoryEnable,
            () => CombinePhysicalMemoryPages(),
            "物理内存页合并完成"
        );
        Execute(
            request.FlushModifiedFileEnable,
            () => FlushFixedDriveCaches(cancellationToken),
            "已刷新磁盘文件缓存"
        );
        Execute(request.ExecuteRegistryEnable, ExecuteRegistryReconciliation, "已刷新注册表缓存");

        if (operationCount == 0)
            progress.Report(new CleanMemoryProgress(100, "没有启用内存清理项目"));

        return ValueTask.FromResult(new RunResult(0, "内存清理完成"));
    }

    private static int CountEnabledOperations(CleanMemoryRequest request) =>
        (request.PurgeStandbyEnable ? 1 : 0)
        + (request.PurgeLowPriorityStandbyEnable ? 1 : 0)
        + (request.FlushModifiedEnable ? 1 : 0)
        + (request.EmptyWorkingSetsEnable ? 1 : 0)
        + (request.PurgeSystemFileEnable ? 1 : 0)
        + (request.CombinePhysicalMemoryEnable ? 1 : 0)
        + (request.FlushModifiedFileEnable ? 1 : 0)
        + (request.ExecuteRegistryEnable ? 1 : 0);

    private static int GetPercentage(int completed, int total) =>
        total == 0 ? 100 : completed * 100 / total;

    private void EmptyRequestedWorkingSingleSets(
        string? programNames,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(programNames))
        {
            EmptyWorkingSets();
            return;
        }

        const uint access =
            NativeProcessMemory.PROCESS_QUERY_LIMITED_INFORMATION
            | NativeProcessMemory.PROCESS_SET_QUOTA;
        var excludeNames = programNames.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        var processes = Process
            .GetProcesses()
            .Where(x => !excludeNames.Contains(x.ProcessName, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        foreach (var process in processes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            cancellationToken.ThrowIfCancellationRequested();
            var processHandle = NativeProcessMemory.OpenProcess(access, false, (uint)process.Id);
            if (processHandle == 0)
            {
                continue;
            }

            try
            {
                if (!NativeProcessMemory.EmptyWorkingSet(processHandle))
                {
                    continue;
                }
            }
            finally
            {
                NativeProcessMemory.CloseHandle(processHandle);
            }
        }
    }

    private void FlushFixedDriveCaches(CancellationToken cancellationToken)
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                FlushModifiedFileCache(drive.Name[0]);
        }
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
