using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ABIRuntime.Abstractions;
using ABIRuntime.Runtime;
using Haiyu.ABI.Services;

namespace Haiyu.ABI;

public static unsafe class CoreExports
{

    /// <summary>
    /// 导出函数
    /// </summary>
    /// <param name="window"></param>
    /// <param name="module"></param>
    /// <param name="commandLine"></param>
    /// <param name="showCommand"></param>
    [UnmanagedCallersOnly(EntryPoint = "ElevatedEntry",
        CallConvs = new[] { typeof(CallConvStdcall) })]
    public static void ElevatedEntry(
        nint window, nint module, byte* commandLine, int showCommand)
    {
        try
        {
            string arguments = Marshal.PtrToStringAnsi((nint) commandLine) ?? string.Empty;
            string[] parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return;

            var services = new PrivilegedServiceRegistry();
            services.Register();
            ElevatedHost.RunAsync(parts[0], parts[1], services)
                .GetAwaiter().GetResult();
        }
        catch
        {
            
        }
    }
}
