namespace Waves.Diff;

/// <summary>A seekable, read-only logical stream with a bounded 16-member LRU handle cache.</summary>
internal sealed class ConcatFileStream : Stream
{
    private readonly (string Path, long Size, long Offset)[] _parts;
    private readonly bool _asynchronous;
    private const int MaxOpenParts = 16;
    private readonly Dictionary<int, LinkedListNode<(int Index, FileStream Stream)>> _openParts = new(MaxOpenParts);
    private readonly LinkedList<(int Index, FileStream Stream)> _lru = new();
    private FileStream? _current;
    private int _currentIndex = -1;
    private long _position;
    private bool _disposed;

    public override bool CanRead => !_disposed;
    public override bool CanSeek => !_disposed;
    public override bool CanWrite => false;
    public override long Length { get; }
    public override long Position
    {
        get { ThrowIfDisposed(); return _position; }
        set { ThrowIfDisposed(); ArgumentOutOfRangeException.ThrowIfNegative(value); _position = value; }
    }

    public ConcatFileStream(IReadOnlyList<string> paths, IReadOnlyList<long> sizes, bool asynchronous = false)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(sizes);
        if (paths.Count != sizes.Count) throw new ArgumentException("paths 与 sizes 数量不一致。");
        _asynchronous = asynchronous;
        _parts = new (string, long, long)[paths.Count];
        long offset = 0;
        for (int i = 0; i < paths.Count; i++)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizes[i]);
            ArgumentNullException.ThrowIfNull(paths[i]);
            _parts[i] = (paths[i], sizes[i], offset);
            offset = checked(offset + sizes[i]);
        }
        Length = offset;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        ThrowIfDisposed();
        int want = (int) Math.Min(buffer.Length, Math.Max(0, Length - _position));
        int total = 0;
        while (total < want)
        {
            int index = FindPart(_position);
            var part = _parts[index];
            var stream = OpenPart(index);
            stream.Position = _position - part.Offset;
            int length = (int) Math.Min(want - total, part.Size - stream.Position);
            int got = stream.Read(buffer.Slice(total, length));
            if (got == 0) throw new EndOfStreamException("拼接流成员文件读取不完整。");
            total += got;
            _position += got;
        }
        return total;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        int want = (int) Math.Min(buffer.Length, Math.Max(0, Length - _position));
        int total = 0;
        while (total < want)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = FindPart(_position);
            var part = _parts[index];
            var stream = OpenPart(index);
            stream.Position = _position - part.Offset;
            int length = (int) Math.Min(want - total, part.Size - stream.Position);
            int got = await stream.ReadAsync(buffer.Slice(total, length), cancellationToken).ConfigureAwait(false);
            if (got == 0) throw new EndOfStreamException("拼接流成员文件读取不完整。");
            total += got;
            _position += got;
        }
        return total;
    }

    private FileStream OpenPart(int index)
    {
        if (index == _currentIndex) return _current!;
        if (_openParts.TryGetValue(index, out var cached))
        {
            _lru.Remove(cached);
            _lru.AddFirst(cached);
            _current = cached.Value.Stream;
            _currentIndex = index;
            return _current;
        }
        // Open before eviction: an inaccessible member must not discard a usable cached handle.
        // Each retained member has a 64 KiB buffer, bounded to 1 MiB for the entire cache.
        var stream = new FileStream(_parts[index].Path, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.SequentialScan | (_asynchronous ? FileOptions.Asynchronous : FileOptions.None));
        try
        {
            if (_openParts.Count == MaxOpenParts)
            {
                var victim = _lru.Last!;
                victim.Value.Stream.Dispose();
                _openParts.Remove(victim.Value.Index);
                _lru.RemoveLast();
            }
            var node = _lru.AddFirst((index, stream));
            _openParts.Add(index, node);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
        _current = stream;
        _currentIndex = index;
        return stream;
    }

    private int FindPart(long position)
    {
        if (_currentIndex >= 0)
        {
            var current = _parts[_currentIndex];
            if (position >= current.Offset && position - current.Offset < current.Size) return _currentIndex;
        }
        int lo = 0, hi = _parts.Length;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (position < _parts[mid].Offset + _parts[mid].Size) hi = mid;
            else lo = mid + 1;
        }
        if (lo == _parts.Length) throw new IOException("拼接流读取位置越界。");
        return lo;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            foreach (var member in _lru)
                member.Stream.Dispose();
            _openParts.Clear();
            _lru.Clear();
            _current = null;
            _currentIndex = -1;
        }
        base.Dispose(disposing);
    }
    public override void Flush() => ThrowIfDisposed();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();
        long position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => checked(_position + offset),
            SeekOrigin.End => checked(Length + offset),
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        if (position < 0) throw new IOException("拼接流读取位置为负数。");
        return _position = position;
    }
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
