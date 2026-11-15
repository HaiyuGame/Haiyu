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
            // 宿主启动失败必须留痕——此前 catch{} 静默吞掉 DI 验证异常，
            // 宿主无声死亡，UI 侧只能看到管道连接超时，根因完全不可见。
            TryWriteHostCrashLog(exception);
        }
    }

    /// <summary>
    /// 宿主启动失败的兜底日志（尽力而为，失败不可再抛）。
    /// 路径固定为 Documents\Waves\appLogs\abi-host-crash.log，与主程序 appLog 同目录。
    /// </summary>
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

    /// <summary>由自动化测试程序调用，验证已发布 NativeAOT DLL 的真实硬件采集和序列化。</summary>
    [UnmanagedCallersOnly(
        EntryPoint = "MonitorSelfTest",
        CallConvs = new[] { typeof(CallConvStdcall) }
    )]
    public static void MonitorSelfTest(nint window, nint module, byte* commandLine, int showCommand)
    {
        string reportPath = (Marshal.PtrToStringAnsi((nint)commandLine) ?? string.Empty)
            .Trim()
            .Trim('"');
        if (string.IsNullOrWhiteSpace(reportPath))
            return;

        try
        {
            int samples = 0;
            int largestPayload = 0;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var services = new ServiceCollection()
                .AddBuiltInServices()
                .BuildServiceProvider(
                    new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }
                );
            using var scope = services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredKeyedService<
                IPrivilegedService<CMonitorRequest, CMonitorProgress, RunResult>
            >(ABIRuntime.Contract.ComputerMonitorContract.Operation);
            var progress = new InlineProgress<CMonitorProgress>(value =>
            {
                int payloadSize = MemoryPack
                    .MemoryPackSerializer.Serialize(value, ABIMemoryPack.Options)
                    .Length;
                largestPayload = Math.Max(largestPayload, payloadSize);
                if (Interlocked.Increment(ref samples) >= 3)
                    timeout.Cancel();
            });

            try
            {
                service
                    .ExecuteAsync(new CMonitorRequest(), progress, timeout.Token)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }

            File.WriteAllText(
                reportPath,
                samples >= 3
                    ? $"PASS|{samples}|{largestPayload}"
                    : $"FAIL|采样不足|{samples}|{largestPayload}"
            );
        }
        catch (Exception exception)
        {
            File.WriteAllText(reportPath, $"FAIL|{exception}");
        }
    }

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
