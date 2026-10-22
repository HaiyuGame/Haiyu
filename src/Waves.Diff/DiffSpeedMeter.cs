using System.Diagnostics;

namespace Waves.Diff;

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
            _bytesPerSecond = _bytesPerSecond == 0
                ? current
                : _bytesPerSecond * 0.7 + current * 0.3;
            _sampleTimestamp = now;
            _sampleBytes = processedBytes;
        }

        return _bytesPerSecond;
    }
}
