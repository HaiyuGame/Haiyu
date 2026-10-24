namespace Waves.Diff.Checksums;

/// <summary>
/// hpatchz 校验器接口：init → update* → finish，finish 输出小端字节序列。
/// </summary>
internal interface IChecksum : IDisposable
{
    void IDisposable.Dispose() { }
    int Size { get; }
    void Reset();
    void Update(ReadOnlySpan<byte> data);
    byte[] Finish();
    void Finish(Span<byte> destination);
    /// <summary>同算法的新实例。</summary>
    IChecksum New();
}
