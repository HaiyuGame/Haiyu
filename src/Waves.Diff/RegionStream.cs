namespace Waves.Diff;

/// <summary>
/// 差分文件内的只读区域窗口。
/// </summary>
internal sealed class RegionStream : Stream
{
    private readonly Stream _base;
    private readonly long _start;
    private readonly long _length;
    private long _position;

    public RegionStream(Stream baseStream, long start, long length)
    {
        if (start < 0 || length < 0 || start > baseStream.Length || length > baseStream.Length - start)
            throw new InvalidDataException("Diff region out of range.");
        _base = baseStream;
        _start = start;
        _length = length;

    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position
    {
        get => _position;
        set { if (value < 0 || value > _length) throw new ArgumentOutOfRangeException(nameof(value)); _position = value; }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        long remain = _length - Position;
        if (remain <= 0)
            return 0;
        _base.Position = _start + _position;
        int n = _base.Read(buffer, offset, (int) Math.Min(count, remain));
        _position += n;
        return n;
    }

    public override int Read(Span<byte> buffer)
    {
        long remain = _length - Position;
        if (remain <= 0)
            return 0;
        _base.Position = _start + _position;
        int n = _base.Read(buffer[..(int) Math.Min(buffer.Length, remain)]);
        _position += n;
        return n;
    }

    // Separate logical positions allow four interleaved clips to share one file handle.
    // Calls on a shared base stream are serialized by the patch loop.
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int count = (int) Math.Min(buffer.Length, _length - _position);
        if (count == 0) return 0;
        _base.Position = _start + _position;
        int n = await _base.ReadAsync(buffer[..count], cancellationToken).ConfigureAwait(false);
        _position += n;
        return n;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override long Seek(long offset, SeekOrigin origin) => origin switch
    {
        SeekOrigin.Begin => Position = offset,
        SeekOrigin.Current => Position += offset,
        SeekOrigin.End => Position = _length + offset,
        _ => throw new ArgumentOutOfRangeException(nameof(origin)),
    };

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
