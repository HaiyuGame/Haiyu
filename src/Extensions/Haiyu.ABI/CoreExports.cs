using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ABI.Models;
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

    /// <summary>由自动化测试程序调用，验证已发布 NativeAOT DLL 的真实硬件采集和序列化。</summary>
    [UnmanagedCallersOnly(EntryPoint = "MonitorSelfTest",
        CallConvs = new[] { typeof(CallConvStdcall) })]
    public static void MonitorSelfTest(
        nint window, nint module, byte* commandLine, int showCommand)
    {
        string reportPath = (Marshal.PtrToStringAnsi((nint)commandLine) ?? string.Empty)
            .Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(reportPath)) return;

        try
        {
            int samples = 0;
            int largestPayload = 0;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var service = new ComputerMonitorService();
            var progress = new InlineProgress<CMonitorProgress>(value =>
            {
                int payloadSize = MemoryPack.MemoryPackSerializer.Serialize(
                    value, ABIMemoryPack.Options).Length;
                largestPayload = Math.Max(largestPayload, payloadSize);
                if (Interlocked.Increment(ref samples) >= 3)
                    timeout.Cancel();
            });

            try
            {
                service.ExecuteAsync(new CMonitorRequest(), progress, timeout.Token)
                    .AsTask().GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
            }

            File.WriteAllText(reportPath, samples >= 3
                ? $"PASS|{samples}|{largestPayload}"
                : $"FAIL|采样不足|{samples}|{largestPayload}");
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
