using System.Buffers;

namespace Waves.Diff;

/// <summary>Bounded, pooled reader: memory usage is independent of decompressed clip size.</summary>
internal sealed class ClipReader : IDisposable
{
    private readonly Stream _stream;
    private readonly byte[] _buffer;
    private readonly CancellationToken _ct;
    private long _remaining;
    private int _position, _end;

    public ClipReader(Stream stream, long length, CancellationToken ct, int bufferSize = 64 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);
        _stream = stream;
        _remaining = length;
        _ct = ct;
        _buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
    }
    public long Remaining => _remaining;
    public byte ReadByte()
    {
        if (_remaining == 0) throw new InvalidDataException("Diff clip exhausted.");
        if (_position == _end)
        {
            _ct.ThrowIfCancellationRequested();
            _end = _stream.Read(_buffer, 0, (int) Math.Min(_buffer.Length, _remaining));
            _position = 0;
            if (_end == 0) throw new EndOfStreamException();
        }
        _remaining--;
        return _buffer[_position++];
    }
    public (ulong Value, int Tag) Unpack(int tagBits)
    {
        byte b = ReadByte();
        ulong value = (ulong) (b & ((1 << (7 - tagBits)) - 1));
        int tag = tagBits == 0 ? 0 : b >> (8 - tagBits);
        if ((b & (1 << (7 - tagBits))) != 0)
            do
            {
                b = ReadByte();
                if (value > (ulong.MaxValue >> 7)) throw new InvalidDataException("Diff varint overflow.");
                value = (value << 7) | (uint) (b & 127);
            } while ((b & 128) != 0);
        return (value, tag);
    }
    public void AddTo(Span<byte> destination)
    {
        if (destination.Length > _remaining) throw new InvalidDataException("Diff extra clip exhausted.");
        while (!destination.IsEmpty)
        {
            if (_position == _end)
            {
                _ct.ThrowIfCancellationRequested();
                _end = _stream.Read(_buffer, 0, (int) Math.Min(_buffer.Length, _remaining));
                _position = 0;
                if (_end == 0) throw new EndOfStreamException();
            }
            int count = Math.Min(destination.Length, _end - _position);
            var source = _buffer.AsSpan(_position, count);
            int i = 0;
            if (System.Numerics.Vector.IsHardwareAccelerated)
                for (; i <= count - System.Numerics.Vector<byte>.Count; i += System.Numerics.Vector<byte>.Count)
                    (new System.Numerics.Vector<byte>(destination.Slice(i)) +
                        new System.Numerics.Vector<byte>(source.Slice(i))).CopyTo(destination.Slice(i));
            for (; i < count; i++) destination[i] = unchecked((byte) (destination[i] + source[i]));
            _position += count;
            _remaining -= count;
            destination = destination[count..];
        }
    }

    public void Skip(long count)
    {
        if (count < 0 || count > _remaining) throw new InvalidDataException("Diff skip out of bounds.");
        while (count > 0)
        {
            if (_position == _end) { ReadByte(); count--; continue; }
            int n = (int) Math.Min(count, _end - _position);
            _position += n;
            _remaining -= n;
            count -= n;
        }
    }
    public void EnsureEnd()
    {
        if (_remaining != 0 || _stream.ReadByte() != -1)
            throw new InvalidDataException("Diff clip length mismatch.");
    }
    public void Dispose()
    {
        try { _stream.Dispose(); }
        finally { ArrayPool<byte>.Shared.Return(_buffer); }
    }
}
