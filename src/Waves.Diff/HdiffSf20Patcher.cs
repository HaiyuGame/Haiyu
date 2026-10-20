using System.Buffers;
using ZstdNet;

namespace Waves.Diff;

/// <summary>
/// HDIFFSF20 single-compressed-stream patching; step metadata is memory-bounded.
/// Binary semantics: sub_140012B90 (header), sub_140012EF0 (steps/covers),
/// sub_140012D20 (alternating zero/literal delta runs). Caller streams remain open.
/// </summary>
internal sealed class HdiffSf20Patcher
{
    private const int CopySize = 1024 * 1024;
    private const int MemoryStepLimit = 8 * 1024 * 1024;
    private sealed record Head(long NewSize, long OldSize, long Covers, long StepSize,
        long RawSize, long CompressedSize, long BodyStart, string Compression);

    public static void Patch(Stream diffFile, string diffName, long start, long end,
        Stream oldStream, Stream outStream, Action<ulong>? onProgress, CancellationToken ct) =>
        Run(diffFile, diffName, start, end, oldStream, outStream, onProgress, ct, false).GetAwaiter().GetResult();

    public static Task PatchAsync(Stream diffFile, string diffName, long start, long end,
        Stream oldStream, Stream outStream, Action<ulong>? onProgress, CancellationToken ct) =>
        Run(diffFile, diffName, start, end, oldStream, outStream, onProgress, ct, true);

    private static InvalidDataException Invalid(string message) => new("HDIFFSF20: " + message);

    private static async Task<Head> ReadHead(Stream file, long start, long end, bool asynchronous, CancellationToken ct)
    {
        if (start < 0 || end < start || end > file.Length) throw Invalid("region out of bounds.");
        byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            int count = (int) Math.Min(4096, end - start);
            file.Position = start;
            await ReadExactly(file, buffer.AsMemory(0, count), asynchronous, ct).ConfigureAwait(false);
            var cursor = new ByteCursor(buffer, 0, count);
            if (!cursor.ReadSpan(10).SequenceEqual("HDIFFSF20&"u8)) throw Invalid("invalid magic.");
            string compression = cursor.ReadUntil(0);
            if (compression is not ("" or "zstd")) throw new NotSupportedException($"HDIFFSF20 compression '{compression}'.");
            long Size()
            {
                ulong value = cursor.Unpack(0).Value;
                if (value > long.MaxValue) throw Invalid("size overflow.");
                return (long) value;
            }
            long newSize = Size(), oldSize = Size(), covers = Size(), stepSize = Size(), rawSize = Size(), compressedSize = Size();
            long bodyStart = start + cursor.Position;
            long storedSize = compressedSize != 0 ? compressedSize : rawSize;
            if (storedSize != end - bodyStart || compressedSize > rawSize ||
                stepSize > Math.Max(newSize, 16 * 1024 * 1024) || stepSize > rawSize)
                throw Invalid("header sizes are inconsistent.");
            if (compressedSize != 0 && compression.Length == 0) throw Invalid("missing compression type.");
            return new Head(newSize, oldSize, covers, stepSize, rawSize, compressedSize, bodyStart, compression);
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private static async Task Run(Stream diffFile, string diffName, long start, long end,
        Stream oldStream, Stream output, Action<ulong>? progress, CancellationToken ct, bool asynchronous)
    {
        ct.ThrowIfCancellationRequested();
        Head h = await ReadHead(diffFile, start, end, asynchronous, ct).ConfigureAwait(false);
        if (oldStream.Length != h.OldSize) throw Invalid($"{diffName}: old data size mismatch.");
        using var region = new RegionStream(diffFile, h.BodyStart, end - h.BodyStart);
        using Stream body = h.CompressedSize == 0 ? region : new DecompressionStream(region);
        using var input = new Reader(body, h.RawSize, asynchronous, ct);
        byte[] transfer = ArrayPool<byte>.Shared.Rent(CopySize);
        long newEnd = 0, oldEnd = 0, coverCount = h.Covers;
        try
        {
            while (coverCount > 0)
            {
                ct.ThrowIfCancellationRequested();
                long coverBytes = await input.Size().ConfigureAwait(false);
                long rleBytes = await input.Size().ConfigureAwait(false);
                if (coverBytes == 0 || coverBytes > h.StepSize || rleBytes > h.StepSize - coverBytes ||
                    coverBytes > input.Remaining || rleBytes > input.Remaining - coverBytes)
                    throw Invalid("step metadata out of bounds.");
                long totalBytes = coverBytes + rleBytes;
                byte[]? metadata = null;
                Stream? storage = null;
                try
                {
                    if (totalBytes <= MemoryStepLimit)
                    {
                        metadata = ArrayPool<byte>.Shared.Rent((int) totalBytes);
                        await input.ReadExactly(metadata.AsMemory(0, (int) totalBytes)).ConfigureAwait(false);
                        storage = new MemoryStream(metadata, 0, (int) totalBytes, false, true);
                    }
                    else
                    {
                        // Large valid steps spill rather than allocating attacker-controlled sizes.
                        storage = new FileStream(Path.Combine(Path.GetTempPath(), "waves-sf-" + Guid.NewGuid().ToString("N")),
                            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 64 * 1024,
                            FileOptions.DeleteOnClose | (asynchronous ? FileOptions.Asynchronous : FileOptions.None));
                        long left = totalBytes;
                        while (left > 0)
                        {
                            int n = (int) Math.Min(CopySize, left);
                            await input.ReadExactly(transfer.AsMemory(0, n)).ConfigureAwait(false);
                            await Write(storage, transfer.AsMemory(0, n), asynchronous, ct).ConfigureAwait(false);
                            left -= n;
                        }
                    }
                    using var covers = new Reader(new RegionStream(storage, 0, coverBytes), coverBytes, asynchronous, ct);
                    using var rleInput = new Reader(new RegionStream(storage, coverBytes, rleBytes), rleBytes, asynchronous, ct);
                    var rle = new Rle0(rleInput);
                    while (covers.Remaining > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (coverCount == 0) throw Invalid("too many covers.");
                        var (delta, sign) = await covers.Unpack(1).ConfigureAwait(false);
                        if (delta > long.MaxValue || (sign != 0 && delta > (ulong) oldEnd) ||
                            (sign == 0 && delta > (ulong) (long.MaxValue - oldEnd)))
                            throw Invalid("old cover position overflow.");
                        long oldPosition = sign == 0 ? oldEnd + (long) delta : oldEnd - (long) delta;
                        long gap = await covers.Size().ConfigureAwait(false);
                        long length = await covers.Size().ConfigureAwait(false);
                        if (gap > h.NewSize - newEnd || length > h.NewSize - newEnd - gap ||
                            (length > 0 && (oldPosition > h.OldSize || length > h.OldSize - oldPosition)))
                            throw Invalid("cover extends beyond data.");
                        coverCount--;
                        if (length == 0 && coverCount != 0) throw Invalid("zero cover is only valid at the end.");
                        long left = gap;
                        while (left > 0)
                        {
                            int n = (int) Math.Min(CopySize, left);
                            await input.ReadExactly(transfer.AsMemory(0, n)).ConfigureAwait(false);
                            await Write(output, transfer.AsMemory(0, n), asynchronous, ct).ConfigureAwait(false);
                            newEnd += n;
                            left -= n;
                            progress?.Invoke((ulong) newEnd);
                        }
                        left = length;
                        if (length > 0) oldStream.Position = oldPosition;
                        while (left > 0)
                        {
                            int n = (int) Math.Min(CopySize, left);
                            await ReadExactly(oldStream, transfer.AsMemory(0, n), asynchronous, ct).ConfigureAwait(false);
                            await rle.Add(transfer.AsMemory(0, n)).ConfigureAwait(false);
                            await Write(output, transfer.AsMemory(0, n), asynchronous, ct).ConfigureAwait(false);
                            newEnd += n;
                            left -= n;
                            progress?.Invoke((ulong) newEnd);
                        }
                        oldEnd = checked(oldPosition + length);
                    }
                    await covers.EnsureEnd().ConfigureAwait(false);
                    await rle.EnsureDrained().ConfigureAwait(false);
                    await rleInput.EnsureEnd().ConfigureAwait(false);
                }
                finally
                {
                    storage?.Dispose();
                    if (metadata is not null) ArrayPool<byte>.Shared.Return(metadata);
                }
            }
            if (newEnd != h.NewSize) throw Invalid("output length mismatch.");
            await input.EnsureEnd().ConfigureAwait(false);
        }
        finally { ArrayPool<byte>.Shared.Return(transfer); }
    }

    private static ValueTask ReadExactly(Stream stream, Memory<byte> buffer, bool asynchronous, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (asynchronous) return stream.ReadExactlyAsync(buffer, ct);
        stream.ReadExactly(buffer.Span);
        return ValueTask.CompletedTask;
    }
    private static ValueTask Write(Stream stream, ReadOnlyMemory<byte> buffer, bool asynchronous, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (asynchronous) return stream.WriteAsync(buffer, ct);
        stream.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    private sealed class Reader : IDisposable
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        private readonly bool _asynchronous;
        private readonly CancellationToken _ct;
        private int _position, _end;
        public long Remaining { get; private set; }
        public Reader(Stream stream, long length, bool asynchronous, CancellationToken ct)
        { _stream = stream; Remaining = length; _asynchronous = asynchronous; _ct = ct; }
        private async ValueTask Fill()
        {
            _ct.ThrowIfCancellationRequested();
            int want = (int) Math.Min(_buffer.Length, Remaining);
            if (want == 0) throw Invalid("clip exhausted.");
            _end = _asynchronous
                ? await _stream.ReadAsync(_buffer.AsMemory(0, want), _ct).ConfigureAwait(false)
                : _stream.Read(_buffer, 0, want);
            _position = 0;
            if (_end == 0) throw new EndOfStreamException("HDIFFSF20 truncated clip.");
        }
        private async ValueTask<byte> Byte()
        {
            if (Remaining == 0) throw Invalid("clip exhausted.");
            if (_position == _end) await Fill().ConfigureAwait(false);
            Remaining--;
            return _buffer[_position++];
        }
        public async ValueTask<(ulong Value, int Sign)> Unpack(int tagBits)
        {
            byte b = await Byte().ConfigureAwait(false);
            int sign = tagBits == 0 ? 0 : b >> (8 - tagBits);
            ulong value = (ulong) (b & ((1 << (7 - tagBits)) - 1));
            if ((b & (1 << (7 - tagBits))) != 0)
                do
                {
                    b = await Byte().ConfigureAwait(false);
                    if (value > (ulong.MaxValue >> 7)) throw Invalid("varint overflow.");
                    value = (value << 7) | (uint) (b & 127);
                } while ((b & 128) != 0);
            return (value, sign);
        }
        public async ValueTask<long> Size()
        {
            ulong value = (await Unpack(0).ConfigureAwait(false)).Value;
            if (value > long.MaxValue) throw Invalid("size overflow.");
            return (long) value;
        }
        public async ValueTask ReadExactly(Memory<byte> destination, bool add = false)
        {
            if (destination.Length > Remaining) throw Invalid("clip exhausted.");
            while (!destination.IsEmpty)
            {
                if (_position == _end) await Fill().ConfigureAwait(false);
                int n = Math.Min(destination.Length, _end - _position);
                if (add)
                {
                    var target = destination.Span;
                    var source = _buffer.AsSpan(_position, n);
                    int i = 0;
                    if (System.Numerics.Vector.IsHardwareAccelerated)
                        for (; i <= n - System.Numerics.Vector<byte>.Count; i += System.Numerics.Vector<byte>.Count)
                            (new System.Numerics.Vector<byte>(target[i..]) + new System.Numerics.Vector<byte>(source[i..])).CopyTo(target[i..]);
                    for (; i < n; i++) target[i] = unchecked((byte) (target[i] + source[i]));
                }
                else _buffer.AsMemory(_position, n).CopyTo(destination);
                _position += n;
                Remaining -= n;
                destination = destination[n..];
            }
        }
        public async ValueTask EnsureEnd()
        {
            if (Remaining != 0) throw Invalid("unused clip bytes.");
            int n = _asynchronous ? await _stream.ReadAsync(_buffer.AsMemory(0, 1), _ct).ConfigureAwait(false) : _stream.Read(_buffer, 0, 1);
            if (n != 0) throw Invalid("decompressed size mismatch.");
        }
        public void Dispose()
        {
            try { _stream.Dispose(); }
            finally { ArrayPool<byte>.Shared.Return(_buffer); }
        }
    }

    private sealed class Rle0(Reader input)
    {
        private long _remaining;
        private bool _zero = false;
        public async ValueTask Add(Memory<byte> destination)
        {
            while (!destination.IsEmpty)
            {
                if (_remaining == 0)
                {
                    _zero = !_zero;
                    _remaining = await input.Size().ConfigureAwait(false);
                    if (!_zero && _remaining > input.Remaining) throw Invalid("RLE literal length exceeds clip.");
                    if (_remaining == 0) continue;
                }
                int n = (int) Math.Min(destination.Length, _remaining);
                if (!_zero) await input.ReadExactly(destination[..n], true).ConfigureAwait(false);
                _remaining -= n;
                destination = destination[n..];
            }
        }
        public async ValueTask EnsureDrained()
        {
            if (_remaining != 0) throw Invalid("unused RLE run.");
            // Upstream finishAppend emits an initial zero run even when no cover bytes exist.
            // Empty trailing runs are complete encodings; any remaining nonempty run is invalid.
            while (input.Remaining > 0)
            {
                _zero = !_zero;
                _remaining = await input.Size().ConfigureAwait(false);
                if (_remaining != 0) throw Invalid("unused RLE run.");
            }
        }
    }
}
