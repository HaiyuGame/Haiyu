using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ABI.Models;
using ABIRuntime.Abstractions;
using ABIRuntime.Runtime;
using Haiyu.ABI.Services;
using Microsoft.Extensions.DependencyInjection;

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
    [UnmanagedCallersOnly(
        EntryPoint = "ElevatedEntry",
        CallConvs = new[] { typeof(CallConvStdcall) }
    )]
    public static void ElevatedEntry(nint window, nint module, byte* commandLine, int showCommand)
    {
        try
        {
            string arguments = Marshal.PtrToStringAnsi((nint)commandLine) ?? string.Empty;
            string[] parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return;
            using var services = new ServiceCollection()
                .AddBuiltInServices()
                .BuildServiceProvider(
                    new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }
                );
            ElevatedHost.RunAsync(parts[0], parts[1], services).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            TryWriteHostCrashLog(exception);
        }
    }

    private static void TryWriteHostCrashLog(Exception exception)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Waves",
                "appLogs"
            );
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "abi-host-crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}"
            );
        }
        catch { }
    }


    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
