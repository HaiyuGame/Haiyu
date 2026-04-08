using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Waves.Core.Models;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Waves.Core.Common;

public class DiffDecompressManagerV2
{
    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetProcessIoCounters(IntPtr ProcessHandle, out IO_COUNTERS IoCounters);

    private string? sharedKey;
    private SharedMemory? _sharedMemory;
    private Process? _process;
    private ManualResetEventSlim? _processExited;

    private ulong _lastTotalBytes;
    private DateTime _lastCheckTime;

    public DiffDecompressManagerV2(string oldFolder, string newFolder, string diffFile)
    {
        OldFolder = oldFolder;
        NewFolder = newFolder;
        DiffFile = diffFile;
    }

    public string OldFolder { get; }
    public string NewFolder { get; }
    public string DiffFile { get; }

    public async Task<int> StartAsync(IProgress<KrDiffDecompressResult> progress)
    {
        try
        {
            _processExited = new ManualResetEventSlim();
            sharedKey = $"launcher_shared_memory_{Process.GetCurrentProcess().Id}_{Guid.NewGuid().ToString("N")}";
            _sharedMemory = new SharedMemory(sharedKey, 4096);

            ProcessStartInfo processStartInfo = new ProcessStartInfo(
                AppDomain.CurrentDomain.BaseDirectory + @"Assets\HpatchzResource\hpatchz.exe",
                new string[6] { OldFolder, DiffFile, NewFolder, "-f", "-d", "-k-" + sharedKey }
            )
            {
                RedirectStandardError = false,
                RedirectStandardOutput = false,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            _process = new Process { StartInfo = processStartInfo, EnableRaisingEvents = true };
            _process.Exited += PatchProgressExitedEventHandler;

            if (_process.Start())
            {
                GetProcessIoCounters(_process.Handle, out IO_COUNTERS initIo);
                _lastTotalBytes = initIo.ReadTransferCount + initIo.WriteTransferCount;
                _lastCheckTime = DateTime.Now;

                while (!_process.HasExited)
                {
                    await Task.Delay(1000);
                    var values = GetProgress(TimeSpan.FromSeconds(1));
                    if (values == null)
                        continue;

                    if (GetProcessIoCounters(_process.Handle, out IO_COUNTERS io))
                    {
                        var now = DateTime.Now;
                        double sec = (now - _lastCheckTime).TotalSeconds;
                        if (sec <= 0)
                        {
                            continue;
                        }

                        ulong currentTotal = io.ReadTransferCount + io.WriteTransferCount;
                        ulong delta = currentTotal - _lastTotalBytes;

                        double speedBytesPerSecond = delta / sec;
                        var result = values.Value;
                        result.SpeedValue = speedBytesPerSecond;

                        _lastTotalBytes = currentTotal;
                        _lastCheckTime = now;
                        progress.Report(result);
                    }
                }
            }

            await _process.WaitForExitAsync();
            return _process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发生异常: {ex.Message}");
            return -10000;
        }
        finally
        {
            _process?.Dispose();
            _process = null;
            _processExited?.Dispose();
            _sharedMemory?.Dispose();
        }
    }

    KrDiffDecompressResult? GetProgress(TimeSpan? timeout = null)
    {
        if (_sharedMemory == null)
        {
            return null;
        }
        int count = 6;
        var result = _sharedMemory.ReadUlong(0, count, out var data, timeout);
        if (result)
        {
            return new KrDiffDecompressResult(data[0], data[1], data[2], data[3], data[4], data[5]);
        }
        return null;
    }

    private void PatchProgressExitedEventHandler(object? sender, EventArgs e)
    {
        _processExited?.Set();
    }
}