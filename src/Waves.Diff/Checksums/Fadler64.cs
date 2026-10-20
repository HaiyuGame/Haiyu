namespace Waves.Diff.Checksums;

/// <summary>
/// hpatchz 私有 fadler64（hpatchz.exe sub_14000FE90 / sub_14000AE20 语义）：
/// 状态 = (s2 &lt;&lt; 32) | s1，初值 s1=1、s2=0；每字节 s1 += T[b]、s2 += s1，均按 2^32 回绕；
/// finish 原样输出 8 字节小端。T 为 hpatchz 内嵌的 256 项 uint32 字节贡献表（非恒等）。
/// </summary>
internal sealed class Fadler64 : IChecksum
{
    private uint _s1 = 1;
    private uint _s2;

    public int Size => 8;

    /// <summary>Append the checksum of an independently initialized suffix without rereading its bytes.</summary>
    internal void Append(Fadler64 suffix, ulong length)
    {
        // Suffix S1 starts at 1; its S2 already includes that 1 for every byte.
        // Only the low 32 length bits matter because both sums wrap modulo 2^32.
        uint suffixS1 = suffix._s1, suffixS2 = suffix._s2;
        unchecked
        {
            _s2 += suffixS2 + (uint)length * (_s1 - 1);
            _s1 += suffixS1 - 1;
        }
    }

    public void Reset()
    {
        _s1 = 1;
        _s2 = 0;
    }

    public void Update(ReadOnlySpan<byte> data)
    {
        var t = Fadler64Table.Values;
        uint s1 = _s1, s2 = _s2;
        int i = 0;
        // Four independent table lookups shorten the byte-at-a-time dependency chain.
        // Arithmetic is deliberately modulo 2^32, matching the file format.
        unchecked
        {
            for (; i <= data.Length - 4; i += 4)
            {
                uint a = t[data[i]], b = t[data[i + 1]];
                uint c = t[data[i + 2]], d = t[data[i + 3]];
                s2 += 4 * s1 + 4 * a + 3 * b + 2 * c + d;
                s1 += a + b + c + d;
            }
            for (; i < data.Length; i++)
            {
                s1 += t[data[i]];
                s2 += s1;
            }
        }
        _s1 = s1;
        _s2 = s2;
    }

    public byte[] Finish()
    {
        var r = new byte[8];
        Finish(r);
        return r;
    }

    public void Finish(Span<byte> destination)
    {
        if (destination.Length < Size) throw new ArgumentException("Checksum destination too small.", nameof(destination));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination, _s1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], _s2);
    }

    public IChecksum New() => new Fadler64();
}
