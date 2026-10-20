namespace Waves.Diff;

internal static class StreamExt
{
    /// <summary>读满 count 字节，不足则抛异常（差分数据不完整属致命错误）；返回值恒等于 count。</summary>
    public static int ReadExact(this Stream s, byte[] buffer, int offset, int count)
    {
        s.ReadAtLeast(buffer.AsSpan(offset, count), count, true);
        return count;
    }
}
