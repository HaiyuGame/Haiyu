using System.Buffers;
using System.Numerics;
using ZstdNet;

using System.Text;

namespace Waves.Diff;

/// <summary>
/// HDIFF13 单文件差分重建（hpatchz.exe sub_1400116A0/sub_1400111B0 逆向语义）。
/// 头布局：\"HDIFF13&\" + 压缩类型 + '\0' + 3 个 varint(newSize, oldSize, coverCount) + 8 个 varint
/// （cover/code/extra/insert 四流各自的 raw/comp 尺寸）；四流按此顺序物理排列，
/// comp!=0 的流用头声明的压缩类型压缩。inc_oldPos 恒为 true（oldPos 随 len 前进，方向位允许回退）。
/// </summary>
internal sealed class Hdiff13Patcher
{
    private sealed record Head(ulong NewSize, ulong OldSize, long CoverCount,
        long CoverRaw, long CoverComp, long CodeRaw, long CodeComp,
        long ExtraRaw, long ExtraComp, long InsertRaw, long InsertComp,
        (long Start, long FileSize) Cover, (long Start, long FileSize) Code,
        (long Start, long FileSize) Extra, (long Start, long FileSize) Insert);

    private readonly Stream _diffFile;
    private readonly string _diffName;
    private readonly long _diffStart; // 区域起点（含头）
    private readonly long _diffEnd;

    private Hdiff13Patcher(Stream diffFile, string diffName, long start, long end)
    {
        _diffFile = diffFile;
        _diffName = diffName;
        _diffStart = start;
        _diffEnd = end;
    }

    private static long ReadSize(ByteCursor c)
    {
        ulong value = c.Unpack(0).Value;
        if (value > long.MaxValue) throw new InvalidDataException("Diff size overflow.");
        return (long) value;
    }

    private Head ParseHead()
    {
        _diffFile.Position = _diffStart;
        var buf = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            int want = (int) Math.Min(buf.Length, _diffEnd - _diffStart);
            _diffFile.ReadAtLeast(buf, want, true);
            return ParseHeadBuffer(buf, want);
        }
        finally { ArrayPool<byte>.Shared.Return(buf); }
    }

    private async Task<Head> ParseHeadAsync(CancellationToken ct)
    {
        _diffFile.Position = _diffStart;
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            int count = (int) Math.Min(64 * 1024, _diffEnd - _diffStart);
            await _diffFile.ReadExactlyAsync(buffer.AsMemory(0, count), ct).ConfigureAwait(false);
            return ParseHeadBuffer(buffer, count);
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private Head ParseHeadBuffer(byte[] buf, int want)
    {
        var c = new ByteCursor(buf, 0, want);


        if (!c.ReadSpan(8).SequenceEqual("HDIFF13&"u8))
            throw new InvalidDataException($"{_diffName}：内嵌差分不是 HDIFF13 格式。");
        string compressType = c.ReadUntil(0);
        if (compressType != "zstd" && compressType != "")
            throw new NotSupportedException(
                $"{_diffName}：内嵌差分压缩类型 \"{compressType}\" 不受支持（生产 krpdiff 仅使用 zstd）。");

        ulong newSize = c.Unpack(0).Value;
        ulong oldSize = c.Unpack(0).Value;
        long coverCount = ReadSize(c);
        if (newSize > long.MaxValue || oldSize > long.MaxValue) throw new InvalidDataException("Data size overflow.");

        Span<long> s = stackalloc long[8];
        for (int i = 0; i < 8; i++)
            s[i] = ReadSize(c);

        long cur = _diffStart + c.Position;
        Span<(long, long)> regions = stackalloc (long, long)[4];
        for (int i = 0; i < 4; i++)
        {
            long fileLen = s[2 * i + 1] != 0 ? s[2 * i + 1] : s[2 * i];
            regions[i] = (cur, fileLen);
            if (fileLen > _diffEnd - cur) throw new InvalidDataException("Diff region out of bounds.");
            if (compressType == "" && s[2 * i + 1] != 0) throw new InvalidDataException("Missing compression type.");
            cur += fileLen;
        }
        if (cur != _diffEnd)
            throw new InvalidDataException($"{_diffName}：内嵌差分四流区域 [{_diffStart},{cur}) 超出区域末尾 {_diffEnd}。");

        return new Head(newSize, oldSize, coverCount,
            s[0], s[1], s[2], s[3], s[4], s[5], s[6], s[7],
            regions[0], regions[1], regions[2], regions[3]);
    }

    /// <summary>
    /// 将区域 [start,end) 的 HDIFF13 差分应用到 oldStream（可 Seek），结果写入 outStream。
    /// progress：每写出 chunk 后以全局已写字节数回调。
    /// </summary>
    public static void Patch(Stream diffFile, string diffName, long start, long end,
        Stream oldStream, Stream outStream, Action<ulong>? onProgress, CancellationToken ct)
    {
        new Hdiff13Patcher(diffFile, diffName, start, end).Run(oldStream, outStream, onProgress, ct);
    }

    public static Task PatchAsync(Stream diffFile, string diffName, long start, long end,
        Stream oldStream, Stream outStream, Action<ulong>? onProgress, CancellationToken ct) =>
        new Hdiff13Patcher(diffFile, diffName, start, end).RunAsync(oldStream, outStream, onProgress, ct);

    private void Run(Stream oldStream, Stream outStream, Action<ulong>? onProgress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Head h = ParseHead();
        if (oldStream.Length != (long) h.OldSize)
            throw new InvalidDataException(
                $"{_diffName}：旧数据尺寸 {oldStream.Length} != 差分声明 oldSize {h.OldSize}。");

        using var coverData = new ClipReader(OpenStream(h.Cover, h.CoverRaw, h.CoverComp), h.CoverRaw, ct);
        using var codeData = new ClipReader(OpenStream(h.Code, h.CodeRaw, h.CodeComp), h.CodeRaw, ct);
        int dataBufferSize = h.NewSize >= 512UL * 1024 * 1024 ? 4 * 1024 * 1024 : 1 * 1024 * 1024;
        int extraBufferSize = h.NewSize >= 512UL * 1024 * 1024 ? 256 * 1024 : 64 * 1024;
        using var extraData = new ClipReader(OpenStream(h.Extra, h.ExtraRaw, h.ExtraComp), h.ExtraRaw, ct, extraBufferSize);

        using var insertStream = OpenStream(h.Insert, h.InsertRaw, h.InsertComp);

        var covers = new CoverReader(coverData, h.CoverCount, h.OldSize, h.NewSize, _diffName);
        var rle = new RleState(codeData, extraData, _diffName);

        var buf = ArrayPool<byte>.Shared.Rent(dataBufferSize);
        try
        {
            ulong newCursor = 0;
            ulong insertRead = 0;

            while (covers.Next(out long oldPos, out ulong newPos, out long len))
            {
                ct.ThrowIfCancellationRequested();

                if (newPos > newCursor)
                {
                    ulong gap = newPos - newCursor;
                    insertRead += CopyInsert(outStream, insertStream, gap, buf, onProgress, ref newCursor, ct);
                    rle.Skip((long) gap);
                }

                long remain = len;
                while (remain > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    int n = (int) Math.Min(dataBufferSize, remain);
                    oldStream.Position = oldPos;
                    oldStream.ReadExact(buf, 0, n);
                    rle.Apply(buf, n);
                    outStream.Write(buf, 0, n);
                    oldPos += n;
                    remain -= n;
                    newCursor += (ulong) n;
                    onProgress?.Invoke(newCursor);
                }
            }

            if ((ulong) h.NewSize > newCursor)
            {
                ulong tail = h.NewSize - newCursor;
                insertRead += CopyInsert(outStream, insertStream, tail, buf, onProgress, ref newCursor, ct);
                rle.Skip((long) tail);
            }

            coverData.EnsureEnd();
            codeData.EnsureEnd();
            extraData.EnsureEnd();
            if (insertStream.ReadByte() != -1) throw new InvalidDataException("Insert clip length mismatch.");
            rle.EnsureDrained();
            covers.EnsureExhausted();

            if (insertRead != (ulong) h.InsertRaw)
                throw new InvalidDataException(
                    $"{_diffName}：insert 流消费 {insertRead} 字节 != 声明 {h.InsertRaw}。");
            if (newCursor != h.NewSize)
                throw new InvalidDataException(
                    $"{_diffName}：重建输出 {newCursor} 字节 != newSize {h.NewSize}。");
        }
        finally { ArrayPool<byte>.Shared.Return(buf); }
    }

    private async Task RunAsync(Stream oldStream, Stream outStream, Action<ulong>? onProgress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Head h = await ParseHeadAsync(ct).ConfigureAwait(false);
        if (oldStream.Length != (long) h.OldSize)
            throw new InvalidDataException(
                $"{_diffName}：旧数据尺寸 {oldStream.Length} != 差分声明 oldSize {h.OldSize}。");

        using var coverData = new ClipReader(OpenStream(h.Cover, h.CoverRaw, h.CoverComp), h.CoverRaw, ct);
        using var codeData = new ClipReader(OpenStream(h.Code, h.CodeRaw, h.CodeComp), h.CodeRaw, ct);
        int dataBufferSize = h.NewSize >= 512UL * 1024 * 1024 ? 4 * 1024 * 1024 : 1 * 1024 * 1024;
        int extraBufferSize = h.NewSize >= 512UL * 1024 * 1024 ? 256 * 1024 : 64 * 1024;
        using var extraData = new ClipReader(OpenStream(h.Extra, h.ExtraRaw, h.ExtraComp), h.ExtraRaw, ct, extraBufferSize);

        using var insertStream = OpenStream(h.Insert, h.InsertRaw, h.InsertComp);

        var covers = new CoverReader(coverData, h.CoverCount, h.OldSize, h.NewSize, _diffName);
        var rle = new RleState(codeData, extraData, _diffName);

        var buf = ArrayPool<byte>.Shared.Rent(dataBufferSize);
        try
        {
            ulong newCursor = 0;
            ulong insertRead = 0;

            while (covers.Next(out long oldPos, out ulong newPos, out long len))
            {
                ct.ThrowIfCancellationRequested();

                if (newPos > newCursor)
                {
                    ulong gap = newPos - newCursor;
                    await CopyInsertAsync(outStream, insertStream, gap, buf, onProgress, newCursor, ct).ConfigureAwait(false);
                    insertRead += gap;
                    newCursor += gap;
                    rle.Skip((long) gap);
                }

                long remain = len;
                while (remain > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    int n = (int) Math.Min(dataBufferSize, remain);
                    oldStream.Position = oldPos;
                    await oldStream.ReadExactlyAsync(buf.AsMemory(0, n), ct).ConfigureAwait(false);
                    rle.Apply(buf, n);
                    await outStream.WriteAsync(buf.AsMemory(0, n), ct).ConfigureAwait(false);
                    oldPos += n;
                    remain -= n;
                    newCursor += (ulong) n;
                    onProgress?.Invoke(newCursor);
                }
            }

            if ((ulong) h.NewSize > newCursor)
            {
                ulong tail = h.NewSize - newCursor;
                await CopyInsertAsync(outStream, insertStream, tail, buf, onProgress, newCursor, ct).ConfigureAwait(false);
                insertRead += tail;
                newCursor += tail;
                rle.Skip((long) tail);
            }

            coverData.EnsureEnd();
            codeData.EnsureEnd();
            extraData.EnsureEnd();
            if (insertStream.ReadByte() != -1) throw new InvalidDataException("Insert clip length mismatch.");
            rle.EnsureDrained();
            covers.EnsureExhausted();

            if (insertRead != (ulong) h.InsertRaw)
                throw new InvalidDataException(
                    $"{_diffName}：insert 流消费 {insertRead} 字节 != 声明 {h.InsertRaw}。");
            if (newCursor != h.NewSize)
                throw new InvalidDataException(
                    $"{_diffName}：重建输出 {newCursor} 字节 != newSize {h.NewSize}。");
        }
        finally { ArrayPool<byte>.Shared.Return(buf); }
    }

    private static ulong CopyInsert(Stream outStream, Stream insertStream, ulong count, byte[] buf,
        Action<ulong>? onProgress, ref ulong cursor, CancellationToken ct)
    {
        ulong remain = count;
        while (remain > 0)
        {
            ct.ThrowIfCancellationRequested();
            int n = (int) Math.Min((ulong) buf.Length, remain);
            insertStream.ReadExact(buf, 0, n);
            outStream.Write(buf, 0, n);
            remain -= (ulong) n;
            cursor += (ulong) n;
            onProgress?.Invoke(cursor);
        }
        return count;
    }

    private static async Task CopyInsertAsync(Stream output, Stream input, ulong count, byte[] buffer,
        Action<ulong>? progress, ulong cursor, CancellationToken ct)
    {
        while (count > 0)
        {
            ct.ThrowIfCancellationRequested();
            int n = (int) Math.Min((ulong) buffer.Length, count);
            await input.ReadExactlyAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
            await output.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
            count -= (ulong) n;
            cursor += (ulong) n;
            progress?.Invoke(cursor);
        }
    }

    /// <summary>insert 大流：压缩时返回流式解压视图，未压缩时返回区域窗口。</summary>
    private Stream OpenStream((long Start, long FileSize) region, long rawSize, long compSize)
    {
        if (compSize == 0)
            return new RegionStream(_diffFile, region.Start, region.FileSize);
        return new DecompressionStream(new RegionStream(_diffFile, region.Start, region.FileSize));
    }

    /// <summary>
    /// cover 迭代器（hpatchz.exe sub_140010E80 语义）。
    /// 每条记录：[tag+oldDelta(varint, tagBits=1, tag=方向)][newDelta(plain)][len(plain)] 交织于同一流。
    /// </summary>
    private struct CoverReader
    {
        private readonly ClipReader _c;
        private readonly long _count;
        private readonly ulong _oldSize;
        private readonly ulong _newSize;
        private readonly string _diffName;
        private long _remaining;
        private long _oldPos;
        private ulong _newPos;

        public CoverReader(ClipReader data, long count, ulong oldSize, ulong newSize, string diffName)
        {
            _c = data;
            _count = count;
            _oldSize = oldSize;
            _newSize = newSize;
            _diffName = diffName;
            _remaining = count;
        }

        public bool Next(out long oldPos, out ulong newPos, out long len)
        {
            if (_remaining == 0)
            {
                oldPos = 0;
                newPos = 0;
                len = 0;
                return false;
            }
            _remaining--;

            var (oldDelta, dir) = _c.Unpack(1);
            var (newDelta, _) = _c.Unpack(0);
            var (l, _) = _c.Unpack(0);

            if (oldDelta > long.MaxValue || l > long.MaxValue ||
                (dir == 0 && oldDelta > (ulong) (long.MaxValue - _oldPos)))
                throw new InvalidDataException("Cover position overflow.");
            long newOldPos = dir != 0 ? _oldPos - (long) oldDelta : _oldPos + (long) oldDelta;
            ulong newNewPos = _newPos + newDelta;

            if (newNewPos < _newPos || newNewPos > _newSize || (ulong) l > _newSize - newNewPos)
                throw new InvalidDataException($"{_diffName}：cover #{_count - _remaining - 1} newPos+len 越界。");
            if (newOldPos < 0 || (ulong) newOldPos > _oldSize || (ulong) l > _oldSize - (ulong) newOldPos)
                throw new InvalidDataException($"{_diffName}：cover #{_count - _remaining - 1} oldPos+len 越界。");

            oldPos = newOldPos;
            newPos = newNewPos;
            len = (long) l;

            // inc_oldPos 恒为 true（hpatchz.exe sub_1400116A0 处 v42=1）
            _oldPos = newOldPos + len;
            _newPos = newNewPos + (ulong) len;
            return true;
        }

        public void EnsureExhausted()
        {
            if (_remaining != 0 || _c.Remaining != 0)
                throw new InvalidDataException(
                    $"{_diffName}：cover 流未耗尽（剩余 {_remaining} 条记录 / {_c.Remaining} 字节）。");
        }
    }

    /// <summary>
    /// RLE 状态机（hpatchz.exe sub_140010CF0/sub_140010B90 语义）。
    /// code 流条目：首字节高 2 位为 mode，len 为 unpackUIntWithTag(tagBits=2) 的值 +1。
    /// mode 0=透传 insert run；1=逐字节 -1；2=逐字节 +const（const 取自 extra 流一字节）；
    /// 3=逐字节 +extra（extra 流逐字节）。生成器契约：add 变换只落在 cover 拷贝区，gap 区恒为 mode 0。
    /// </summary>
    private sealed class RleState
    {
        private readonly ClipReader _code;
        private readonly ClipReader _extra;
        private readonly string _diffName;
        private long _insertRemain;   // 当前 insert run 剩余（mode 0/1/2）
        private long _addRemain;      // 当前逐字节 add run 剩余（mode 3）
        private int _addByte;         // insert run 的常量加数（有符号语义），0=不加

        public RleState(ClipReader code, ClipReader extra, string diffName)
        {
            _code = code;
            _extra = extra;
            _diffName = diffName;
        }

        private (long Len, int Mode) ReadEntry()
        {
            if (_code.Remaining == 0)
                throw new InvalidDataException($"{_diffName}：RLE code 流在数据未完成时耗尽。");
            var (lenRaw, mode) = _code.Unpack(2); // 首字节高 2 位 = mode，低 5 位 + 续组 = len-1
            if (lenRaw >= long.MaxValue) throw new InvalidDataException("RLE length overflow.");
            long len = (long) lenRaw + 1;
            switch (mode)
            {
                case 0:
                    _addByte = 0;
                    _insertRemain = len;
                    break;
                case 1:
                    _addByte = -1;
                    _insertRemain = len;
                    break;
                case 2:
                    _addByte = (sbyte) _extra.ReadByte();
                    _insertRemain = len;
                    break;
                case 3:
                    _addRemain = len;
                    break;
            }
            return (len, mode);
        }

        /// <summary>对 buffer 应用变换（cover 拷贝路径）。</summary>
        public void Apply(byte[] buf, int len)
        {
            int done = 0;
            while (done < len)
            {
                if (_insertRemain > 0)
                {
                    int n = (int) Math.Min(_insertRemain, len - done);
                    if (_addByte != 0)
                    {
                        var span = buf.AsSpan(done, n);
                        int i = 0;
                        if (Vector.IsHardwareAccelerated)
                        {
                            var add = new Vector<byte>(unchecked((byte) _addByte));
                            for (; i <= n - Vector<byte>.Count; i += Vector<byte>.Count)
                                (new Vector<byte>(span[i..]) + add).CopyTo(span[i..]);
                        }
                        for (; i < n; i++) span[i] = unchecked((byte) (span[i] + _addByte));
                    }
                    done += n;
                    _insertRemain -= n;
                }
                else if (_addRemain > 0)
                {
                    int n = (int) Math.Min(_addRemain, len - done);
                    _extra.AddTo(buf.AsSpan(done, n));
                    done += n;
                    _addRemain -= n;
                }
                else if (_code.Remaining > 0)
                {
                    ReadEntry();
                }
                else
                {
                    throw new InvalidDataException($"{_diffName}：RLE 变换在数据未完成时耗尽。");
                }
            }
        }

        /// <summary>跳过 count 字节的变换（gap 路径：只推进状态与 extra 消耗，不产出）。</summary>
        public void Skip(long count)
        {
            long done = 0;
            while (done < count)
            {
                if (_insertRemain > 0)
                {
                    long n = Math.Min(_insertRemain, count - done);
                    done += n;
                    _insertRemain -= n;
                }
                else if (_addRemain > 0)
                {
                    long n = Math.Min(_addRemain, count - done);
                    _extra.Skip(n);
                    done += n;
                    _addRemain -= n;
                }
                else if (_code.Remaining > 0)
                {
                    ReadEntry();
                }
                else
                {
                    throw new InvalidDataException($"{_diffName}：RLE 跳过在数据未完成时耗尽。");
                }
            }
        }

        /// <summary>全部产出完成后：所有 run 与两流必须恰好耗尽（sub_1400111B0 收尾对账）。</summary>
        public void EnsureDrained()
        {
            if (_insertRemain != 0 || _addRemain != 0 || _code.Remaining != 0 || _extra.Remaining != 0)
                throw new InvalidDataException(
                    $"{_diffName}：RLE 未耗尽（insertRemain={_insertRemain}, addRemain={_addRemain}, " +
                    $"code 剩 {_code.Remaining}, extra 剩 {_extra.Remaining}）。");
        }
    }
}
