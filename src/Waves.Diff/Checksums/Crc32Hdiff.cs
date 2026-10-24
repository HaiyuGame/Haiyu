namespace Waves.Diff.Checksums;

/// <summary>
/// hpatchz crc32（hpatchz.exe sub_14000AD20/AD30/ADC0 语义）：
/// 标准 IEEE CRC32（sub_140020370 对每次输入/输出取反），公开初值 0，finish 输出 4 字节小端。
/// </summary>
internal sealed class Crc32Hdiff : IChecksum
{
    private static readonly uint[] Table = BuildTable();
    private uint _v;

    public int Size => 4;

    private static uint[] BuildTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }

    public void Reset() => _v = 0;

    public void Update(ReadOnlySpan<byte> data)
    {
        uint v = ~_v;
        for (int i = 0; i < data.Length; i++)
            v = Table[(v ^ data[i]) & 0xFF] ^ (v >> 8);
        _v = ~v;
    }

    public byte[] Finish()
    {
        var r = new byte[4];
        Finish(r);
        return r;
    }

    public void Finish(Span<byte> destination) =>
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination, _v);

    public IChecksum New() => new Crc32Hdiff();
}
