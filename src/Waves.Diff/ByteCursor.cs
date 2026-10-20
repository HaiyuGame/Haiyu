using System.Text;

namespace Waves.Diff;

/// <summary>
/// 大端 7 位组 varint 读取游标（hpatchz sub_1400103A0 语义）。
/// 首字节：高 tagBits 位为 tag，第 (7-tagBits) 位为续位，低 (7-tagBits) 位为数据；
/// 后续字节：第 7 位为续位，低 7 位为数据。
/// </summary>
internal sealed class ByteCursor
{
    private readonly byte[] _buf;
    private int _pos;
    private readonly int _end;

    public ByteCursor(byte[] buf, int offset, int length)
    {
        _buf = buf;
        _pos = offset;
        _end = offset + length;
    }

    public int Position => _pos;
    public long Remaining => _end - _pos;

    private void Ensure(int n)
    {
        if (n < 0 || _end - _pos < n)
            throw new InvalidDataException($"hdiff 数据不足：需要 {n} 字节，剩余 {Remaining} 字节。");
    }

    public byte ReadByte()
    {
        Ensure(1);
        return _buf[_pos++];
    }

    public ReadOnlySpan<byte> ReadSpan(int n)
    {
        Ensure(n);
        var result = _buf.AsSpan(_pos, n);
        _pos += n;
        return result;
    }

    public void Skip(int n) { Ensure(n); _pos += n; }

    public byte[] ReadBytes(int n)
    {
        Ensure(n);
        var r = new byte[n];
        Array.Copy(_buf, _pos, r, 0, n);
        _pos += n;
        return r;
    }

    /// <summary>读取以指定字节结尾的 NUL 感知字符串（不消费结尾字节）。</summary>
    public string ReadUntil(byte terminator)
    {
        int start = _pos;
        while (_pos < _end && _buf[_pos] != terminator)
            _pos++;
        if (_pos >= _end)
            throw new InvalidDataException($"hdiff 类型字符串未找到结束符 0x{terminator:X2}。");
        var s = Encoding.UTF8.GetString(_buf, start, _pos - start);
        _pos++; // 消费结束符
        return s;
    }

    /// <summary>读取 varint。tagBits=0..2；返回 (值, tag 位)。</summary>
    public (ulong Value, int Tag) Unpack(int tagBits)
    {
        Ensure(1);
        byte b = _buf[_pos++];
        ulong v = b & ((1UL << (7 - tagBits)) - 1);
        int tag = b >> (8 - tagBits);
        if ((b & (1 << (7 - tagBits))) != 0)
        {
            for (; ; )
            {
                Ensure(1);
                b = _buf[_pos++];
                if (v > (ulong.MaxValue >> 7))
                    throw new InvalidDataException("hdiff varint overflow.");
                v = (v << 7) | (uint) (b & 0x7F);
                if ((b & 0x80) == 0)
                    break;
            }
        }
        return (v, tag);
    }
}
