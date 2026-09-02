using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ABI.Models;
using Haiyu.ABI.Common.Navtive;
using Haiyu.ABI.Services;

namespace KuroGameDownloadProgram.Tests
{
    public static class ABITest
    {
        public static async Task MonitorTest()
        {
            ComputerMonitorService monitor = new ComputerMonitorService();
            var a = await monitor.ExecuteAsync(
                new CMonitorRequest(),
                new Progress<CMonitorProgress>(s =>
                {
                    Console.WriteLine(s);
                }),
                default
            );
        }

        public static async Task CleanMemoryTest()
        {
            MemoryCleanerService service = new();

            NativePrivilege.EnablePrivilege("SeProfileSingleProcessPrivilege");

            // 1. 清理低优先级待机内存
            service.PurgeLowPriorityStandbyList();

            await Task.Delay(1000);

            // 2. 清理全部待机内存
            service.PurgeStandbyList();

            await Task.Delay(1000);

            // 3. 将 Modified 内存页写回
            service.FlushModifiedPageList();

            await Task.Delay(1000);

            // 4. 收缩系统进程工作集
            service.EmptyWorkingSets();
        }

        public static async Task CleanSingleProcessId(uint processId)
        {
            const uint access =
                NativeProcessMemory.PROCESS_QUERY_LIMITED_INFORMATION
                | NativeProcessMemory.PROCESS_SET_QUOTA;

            var processHandle = NativeProcessMemory.OpenProcess(access, false, processId);

            if (processHandle == 0)
            {
                throw new InvalidOperationException(
                    $"OpenProcess failed. PID: {processId}, Win32Error: {Marshal.GetLastWin32Error()}"
                );
            }

            try
            {
                if (!NativeProcessMemory.EmptyWorkingSet(processHandle))
                {
                    throw new InvalidOperationException(
                        $"EmptyWorkingSet failed. PID: {processId}, Win32Error: {Marshal.GetLastWin32Error()}"
                    );
                }
            }
            finally
            {
                NativeProcessMemory.CloseHandle(processHandle);
            }
        }

        public static async Task CleanSystemFileTest()
        {
            MemoryCleanerService service = new();

            NativePrivilege.EnablePrivilege("SeIncreaseQuotaPrivilege");

            service.PurgeSystemFileCache();

            await Task.CompletedTask;
        }

        public static async Task CleanSeProfileSingleProcessTest()
        {
            MemoryCleanerService service = new();

            NativePrivilege.EnablePrivilege("SeProfileSingleProcessPrivilege");

            var pages = service.CombinePhysicalMemoryPages();

            Debug.WriteLine($"合并页面数量：{pages}");

            await Task.CompletedTask;
        }

        public static async Task FlushDiskMemory()
        {
            MemoryCleanerService service = new();

            service.FlushModifiedFileCache('D');

            await Task.CompletedTask;
        }
    }
}
