using System.Runtime.InteropServices;

namespace ABIRuntime.Runtime;

internal static partial class NativeMethods
{
    internal const uint TokenQuery = 0x0008;
    internal const int TokenElevation = 20;
    internal const uint SeeMaskNoCloseProcess = 0x00000040;
    internal const int SwHide = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct ShellExecuteInfo
    {
        internal int cbSize;
        internal uint fMask;
        internal nint hwnd;
        [MarshalAs(UnmanagedType.LPWStr)] internal string? lpVerb;
        [MarshalAs(UnmanagedType.LPWStr)] internal string? lpFile;
        [MarshalAs(UnmanagedType.LPWStr)] internal string? lpParameters;
        [MarshalAs(UnmanagedType.LPWStr)] internal string? lpDirectory;
        internal int nShow;
        internal nint hInstApp;
        internal nint lpIDList;
        [MarshalAs(UnmanagedType.LPWStr)] internal string? lpClass;
        internal nint hkeyClass;
        internal uint dwHotKey;
        internal nint hIconOrMonitor;
        internal nint hProcess;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TokenElevationInfo
    {
        internal int TokenIsElevated;
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint handle);

    [LibraryImport("kernel32.dll")]
    internal static partial nint GetCurrentProcess();

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool OpenProcessToken(
        nint process, uint access, out nint token);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool GetTokenInformation(
        nint token,
        int tokenClass,
        void* tokenInformation,
        uint tokenInformationLength,
        out uint returnLength);

    [DllImport("shell32.dll", EntryPoint = "ShellExecuteExW",
        SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShellExecuteEx(ref ShellExecuteInfo info);
}
