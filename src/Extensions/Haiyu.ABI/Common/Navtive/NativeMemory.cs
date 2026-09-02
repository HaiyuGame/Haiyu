using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Haiyu.ABI.Common;

internal static partial class NativeMemory
{
    internal const int SystemMemoryListInformation = 80;
    internal const int SystemCombinePhysicalMemoryInformation = 130;
    internal const int SystemRegistryReconciliationInformation = 155;
    internal const uint GENERIC_READ = 0x80000000;
    internal const uint GENERIC_WRITE = 0x40000000;
    internal const uint FILE_SHARE_READ = 0x00000001;
    internal const uint FILE_SHARE_WRITE = 0x00000002;
    internal const uint OPEN_EXISTING = 3;
    internal static readonly nint INVALID_HANDLE_VALUE = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryCombineInformation
    {
        internal nint Handle;
        internal nuint PagesCombined;
        internal uint Flags;
    }

    [LibraryImport("ntdll.dll")]
    internal static partial int NtSetSystemInformation(
        int systemInformationClass,
        ref MemoryCombineInformation systemInformation,
        uint systemInformationLength
    );

    /// <summary>
    /// 内核API
    /// </summary>
    /// <param name="systemInformationClass"></param>
    /// <param name="systemInformation"></param>
    /// <param name="systemInformationLength"></param>
    /// <returns></returns>
    [LibraryImport("ntdll.dll")]
    internal static partial int NtSetSystemInformation(
        int systemInformationClass,
        ref SystemMemoryListCommand systemInformation,
        uint systemInformationLength
    );

    [LibraryImport("ntdll.dll")]
    internal static partial int NtSetSystemInformation(
        int systemInformationClass,
        nint systemInformation,
        uint systemInformationLength
    );

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        StringMarshalling = StringMarshalling.Utf16,
        SetLastError = true
    )]
    internal static partial nint CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        nint lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        nint hTemplateFile
    );

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FlushFileBuffers(nint hFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint hObject);

    /// <summary>
    /// 清理命令枚举
    /// </summary>
    internal enum SystemMemoryListCommand : int
    {
        MemoryCaptureAccessedBits = 0,
        MemoryCaptureAndResetAccessedBits = 1,

        /// <summary>
        /// 清理系统中各进程的 Working Set。
        /// </summary>
        MemoryEmptyWorkingSets = 2,

        /// <summary>
        /// Flush Modified Page List。
        /// </summary>
        MemoryFlushModifiedList = 3,

        /// <summary>
        /// 清理整个 Standby List。
        /// </summary>
        MemoryPurgeStandbyList = 4,

        /// <summary>
        /// 仅清理低优先级 Standby List。
        /// </summary>
        MemoryPurgeLowPriorityStandbyList = 5,
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetSystemFileCacheSize(
        nuint MinimumFileCacheSize,
        nuint MaximumFileCacheSize,
        uint Flags
    );

    /// <summary>
    /// NT内核执行结果
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    internal static bool NtSuccess(int status)
    {
        return status >= 0;
    }

    internal static int ExecuteMemoryCommand(SystemMemoryListCommand command)
    {
        return NtSetSystemInformation(
            SystemMemoryListInformation,
            ref command,
            sizeof(SystemMemoryListCommand)
        );
    }

    internal static int ExecuteCombineMemory(ref MemoryCombineInformation information)
    {
        return NtSetSystemInformation(
            SystemCombinePhysicalMemoryInformation,
            ref information,
            (uint)Marshal.SizeOf<MemoryCombineInformation>()
        );
    }

    internal static int ExecuteRegistryReconciliation()
    {
        return NtSetSystemInformation(
            SystemRegistryReconciliationInformation,
            0,
            0);
    }
}
