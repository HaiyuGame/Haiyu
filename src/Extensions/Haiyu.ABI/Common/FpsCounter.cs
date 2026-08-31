using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using ABI.Models;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace Haiyu.ABI.Common;

public sealed partial class FpsCounter : IDisposable, IAsyncDisposable
{
    public const int EventID_D3D9PresentStart = 1;
    public const int EventID_DxgiPresentStart = 42;

    public static readonly Guid DXGI_provider = Guid.Parse(
        "{CA11C036-0102-4A2D-A6AD-F03CFED5D3C9}"
    );

    public static readonly Guid D3D9_provider = Guid.Parse(
        "{783ACA0A-790E-4D7F-8451-AA850511C6B9}"
    );

    public Dictionary<int, TimestampCollection> Frames { get; } = new();

    public Action<FPSData>? FpsOutput;

    private readonly object sync = new();

    private readonly Channel<PresentEvent> eventChannel = Channel.CreateUnbounded<PresentEvent>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false,
        }
    );

    private TraceEventSession? m_EtwSession;

    private CancellationTokenSource? cancellationTokenSource;

    private Task? etwTask;
    private Task? consumerTask;
    private Task? outputTask;

    private bool disposedValue;
    private bool started;

    private volatile bool pause;

    /// <summary>
    /// 输出刷新间隔。
    /// 实时 FPS 使用最近 1 秒，帧时间统计使用最近 30 秒。
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    public bool Pause
    {
        get => pause;
        set => pause = value;
    }

    /// <summary>
    /// 保留原来的 Start() API。
    /// </summary>
    public void Start()
    {
        StartAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// 新增异步启动接口。
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);

        if (started)
            return Task.CompletedTask;

        started = true;

        cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );

        var token = cancellationTokenSource.Token;

        m_EtwSession = new TraceEventSession(
            $"Haiyu.Fps.{Environment.ProcessId}.{Guid.NewGuid():N}"
        );

        m_EtwSession.StopOnDispose = true;

        m_EtwSession.EnableProvider("Microsoft-Windows-D3D9");

        m_EtwSession.EnableProvider("Microsoft-Windows-DXGI");

        m_EtwSession.Source.AllEvents += Source_AllEvents;

        etwTask = Task.Run(() => EtwThreadProc(token), CancellationToken.None);

        consumerTask = ConsumeEventsAsync(token);

        outputTask = OutputThreadProcAsync(token);

        return Task.CompletedTask;
    }

    private void EtwThreadProc(CancellationToken cancellationToken)
    {
        try
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                m_EtwSession?.Source.Process();
            }
        }
        catch (ObjectDisposedException) { }
        catch (Exception) when (cancellationToken.IsCancellationRequested) { }
    }

    private void Source_AllEvents(TraceEvent obj)
    {
        try
        {
            if (cancellationTokenSource?.IsCancellationRequested != false)
            {
                return;
            }

            bool isPresent =
                ((int)obj.ID == EventID_D3D9PresentStart && obj.ProviderGuid == D3D9_provider)
                || ((int)obj.ID == EventID_DxgiPresentStart && obj.ProviderGuid == DXGI_provider);

            if (!isPresent)
                return;

            int pid = obj.ProcessID;

            if (pid <= 0)
                return;

            double timestamp = obj.TimeStampRelativeMSec;

            eventChannel.Writer.TryWrite(new PresentEvent(pid, timestamp));
        }
        catch
        {
            //
            // ETW callback 不向外传播异常。
            //
        }
    }

    private async Task ConsumeEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var presentEvent in eventChannel.Reader.ReadAllAsync(cancellationToken))
            {
                TimestampCollection collection;

                lock (sync)
                {
                    if (!Frames.TryGetValue(presentEvent.ProcessId, out collection!))
                    {
                        string name = GetProcessName(presentEvent.ProcessId);

                        collection = new TimestampCollection(presentEvent.ProcessId, name);

                        Frames[presentEvent.ProcessId] = collection;
                    }
                }

                collection.Add(presentEvent.Timestamp);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task OutputThreadProcAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (Pause)
                    continue;

                int processId = (int)GetWindowDetails();

                if (processId <= 0)
                    continue;

                TimestampCollection? frames;

                lock (sync)
                {
                    Frames.TryGetValue(processId, out frames);
                }

                if (frames is null)
                    continue;

                if (!frames.TryGetLatestTimestamp(out double to))
                {
                    continue;
                }

                double[] timestamps = frames.Query(to - 30_000, to);
                FpsOutput?.Invoke(CreateFpsData(frames.Name, timestamps, to));
            }
        }
        catch (OperationCanceledException) { }
    }

    private static FPSData CreateFpsData(string processName, double[] timestamps, double to)
    {
        int currentFps = timestamps.Count(timestamp => timestamp >= to - 1000);
        if (timestamps.Length < 2)
        {
            return new FPSData
            {
                ForgroundProgramName = processName,
                FOrgroundProgramFps = currentFps,
                SampleFrameCount = timestamps.Length,
            };
        }

        var frameTimes = new double[timestamps.Length - 1];
        for (int i = 1; i < timestamps.Length; i++)
        {
            frameTimes[i - 1] = Math.Max(0, timestamps[i] - timestamps[i - 1]);
        }

        double duration = timestamps[^1] - timestamps[0];
        double averageFrameTime = frameTimes.Average();
        double[] sorted = [.. frameTimes.Order()];
        double p95 = Percentile(sorted, 0.95);
        double p99 = Percentile(sorted, 0.99);
        double p999 = Percentile(sorted, 0.999);
        double median = Percentile(sorted, 0.5);
        double variance = frameTimes.Average(value =>
        {
            double delta = value - averageFrameTime;
            return delta * delta;
        });
        double stutterThreshold = Math.Max(20, median * 2.5);

        return new FPSData
        {
            ForgroundProgramName = processName,
            FOrgroundProgramFps = currentFps,
            CurrentFrameTime = frameTimes[^1],
            AverageFps = duration > 0 ? frameTimes.Length * 1000d / duration : 0,
            AverageFrameTime = averageFrameTime,
            Low1PercentFps = p99 > 0 ? 1000d / p99 : 0,
            Low01PercentFps = p999 > 0 ? 1000d / p999 : 0,
            FrameTimeP95 = p95,
            FrameTimeP99 = p99,
            FrameTimeP999 = p999,
            MaxFrameTime = sorted[^1],
            FrameTimeStandardDeviation = Math.Sqrt(variance),
            SlowFrameCount = frameTimes.Count(value => value > 1000d / 30d),
            StutterCount = frameTimes.Count(value => value > stutterThreshold),
            SampleFrameCount = timestamps.Length,
            SampleDurationSeconds = duration / 1000d,
            RecentFrameTimes = frameTimes.Length <= 240 ? frameTimes : frameTimes[^240..],
        };
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
            return 0;

        double rank = (sortedValues.Length - 1) * percentile;
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);
        if (lower == upper)
            return sortedValues[lower];

        return sortedValues[lower] + ((sortedValues[upper] - sortedValues[lower]) * (rank - lower));
    }

    private static string GetProcessName(int processId)
    {
        try
        {
            using var proc = Process.GetProcessById(processId);

            return proc.ProcessName;
        }
        catch
        {
            return processId.ToString();
        }
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    /// <summary>
    /// 保留你原来的方法名称。
    /// </summary>
    public static uint GetWindowDetails()
    {
        nint windowHandle = GetForegroundWindow();

        if (windowHandle == nint.Zero)
            return 0;

        uint processId = 0;

        uint threadId = GetWindowThreadProcessId(windowHandle, out processId);

        if (threadId == 0)
            return 0;

        return processId;
    }

    /// <summary>
    /// 保留原来的 Stop()。
    /// </summary>
    public void Stop()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    public async Task StopAsync()
    {
        if (!started)
            return;

        started = false;

        var cts = cancellationTokenSource;

        cancellationTokenSource = null;

        try
        {
            cts?.Cancel();
        }
        catch { }

        if (m_EtwSession is not null)
        {
            try
            {
                m_EtwSession.Source.AllEvents -= Source_AllEvents;
            }
            catch { }

            try
            {
                m_EtwSession.Dispose();
            }
            catch { }

            m_EtwSession = null;
        }

        var tasks = new[] { etwTask, consumerTask, outputTask };

        try
        {
            await Task.WhenAll(tasks.Where(x => x is not null)!);
        }
        catch (OperationCanceledException) { }
        catch { }

        etwTask = null;
        consumerTask = null;
        outputTask = null;

        cts?.Dispose();
    }

    public void Dispose(bool disposing)
    {
        if (disposedValue)
            return;

        if (disposing)
        {
            Stop();
        }

        disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposedValue)
            return;

        await StopAsync();

        disposedValue = true;

        GC.SuppressFinalize(this);
    }

    private readonly record struct PresentEvent(int ProcessId, double Timestamp);
}
