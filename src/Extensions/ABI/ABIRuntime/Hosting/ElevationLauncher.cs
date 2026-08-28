using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ABIRuntime.Runtime;

internal static class ElevationLauncher
{
    internal static void Start(string pipeName, string secret, string coreDllPath)
    {
        string rundll32 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "rundll32.exe");
        string coreDll = Path.IsPathFullyQualified(coreDllPath)
            ? Path.GetFullPath(coreDllPath)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, coreDllPath));

        if (!File.Exists(rundll32))
            throw new FileNotFoundException("找不到 rundll32.exe。", rundll32);
        if (!File.Exists(coreDll))
            throw new FileNotFoundException("找不到 NativeAOT Core DLL。", coreDll);

        string arguments = $"\"{coreDll}\",ElevatedEntry {pipeName} {secret}";
        var info = new NativeMethods.ShellExecuteInfo
        {
            cbSize = Marshal.SizeOf<NativeMethods.ShellExecuteInfo>(),
            fMask = NativeMethods.SeeMaskNoCloseProcess,
            lpVerb = "runas",
            lpFile = rundll32,
            lpParameters = arguments,
            lpDirectory = AppContext.BaseDirectory,
            nShow = NativeMethods.SwHide,
        };

        if (!NativeMethods.ShellExecuteEx(ref info))
            throw new Win32Exception();
        if (info.hProcess != 0)
            NativeMethods.CloseHandle(info.hProcess);
    }
}
