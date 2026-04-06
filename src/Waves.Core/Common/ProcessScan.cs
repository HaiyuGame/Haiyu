using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Waves.Core.Common;

/// <summary>
/// 进程监控与扫描
/// </summary>
public static class ProcessScan
{
    public enum CREATE_TOOLHELP_SNAPSHOT_FLAGS : uint
    {
        TH32CS_SNAPPROCESS = 0x00000002
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(CREATE_TOOLHELP_SNAPSHOT_FLAGS dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Process32FirstW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Process32NextW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    public static bool CheckGameAliveWithWin32(string exeName, uint pid, out bool contained, out uint ppid,out string filePath)
    {
        contained = false;
        ppid = 0u;
        filePath = "";
        IntPtr hSnapshot = IntPtr.Zero;
        try
        {
            hSnapshot = CreateToolhelp32Snapshot(CREATE_TOOLHELP_SNAPSHOT_FLAGS.TH32CS_SNAPPROCESS, 0);
            if (hSnapshot == IntPtr.Zero)
                return false;

            PROCESSENTRY32W lppe = new PROCESSENTRY32W
            {
                dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32W))
            };

            if (!Process32FirstW(hSnapshot, ref lppe))
                return false;

            do
            {
                filePath = GetProcessPath(lppe.th32ProcessID) ?? "";
                if(lppe.szExeFile == exeName && pid == lppe.th32ProcessID)
                {
                    contained = true;
                    ppid = lppe.th32ParentProcessID;
                    return true;
                }
            }
            while (Process32NextW(hSnapshot, ref lppe));

            return true;
        }
        finally
        {
            if (hSnapshot != IntPtr.Zero)
                CloseHandle(hSnapshot);
        }
    }

    public static string GetProcessPath(uint processId)
    {
        IntPtr hProcess = IntPtr.Zero;
        try
        {
            hProcess = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, processId);
            if (hProcess == IntPtr.Zero)
                return null;
            StringBuilder pathBuilder = new StringBuilder(260);
            uint result = GetModuleFileNameEx(hProcess, IntPtr.Zero, pathBuilder, pathBuilder.Capacity);
            if (result > 0)
                return pathBuilder.ToString();
            return null;
        }
        finally
        {
            if (hProcess != IntPtr.Zero)
                CloseHandle(hProcess);
        }
    }


    [Flags]
    public enum ProcessAccessFlags : uint
    {
        QueryLimitedInformation = 0x00001000
    }

    [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
}
