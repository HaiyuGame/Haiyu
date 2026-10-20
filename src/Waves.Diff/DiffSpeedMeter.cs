using System.Diagnostics;

namespace Waves.Diff;

/// <summary>
/// 根据补丁引擎实际提交的输出字节计算处理速度，不读取进程或系统 I/O 计数器。
/// 短间隔进度先累计，避免对每个 1 MiB 块进行高抖动除法；结束时补一次采样。
/// </summary>
internal sealed class DiffSpeedMeter
{
    private static readonly long MinimumSampleTicks = Math.Max(1, Stopwatch.Frequency / 5);
    private long _sampleTimestamp = Stopwatch.GetTimestamp();
    private ulong _sampleBytes;
    private double _bytesPerSecond;

    public double Sample(ulong processedBytes, bool completed = false)
    {
        long now = Stopwatch.GetTimestamp();
        if (processedBytes < _sampleBytes)
        {
            // 允许同一计量器重新开始，防止无符号减法回绕。
            _sampleTimestamp = now;
            _sampleBytes = processedBytes;
            _bytesPerSecond = 0;
            return 0;
        }

        ulong deltaBytes = processedBytes - _sampleBytes;
        long deltaTicks = now - _sampleTimestamp;
        if (deltaBytes != 0 && (deltaTicks >= MinimumSampleTicks || completed))
        {
            double seconds = (double)deltaTicks / Stopwatch.Frequency;
            double current = deltaBytes / seconds;
            // 第一次直接使用实测值；后续轻度平滑，兼顾响应速度和界面稳定性。
            _bytesPerSecond = _bytesPerSecond == 0
                ? current
                : _bytesPerSecond * 0.7 + current * 0.3;
            _sampleTimestamp = now;
            _sampleBytes = processedBytes;
        }

        return _bytesPerSecond;
    }
}
