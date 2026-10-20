using System.Buffers;
using System.Text;

namespace Waves.Diff;

/// <summary>
/// HDIFF19 目录差分头（hpatchz.exe sub_140002970 逆向语义）。
/// 布局：\"HDIFF19&\" + 压缩类型 + '&' + 校验类型 + '\0' + 19 个大端 varint + 4×checksumByteSize 内嵌校验。
/// </summary>
internal sealed class Hdiff19Head
{
    public required string CompressType { get; init; }
    public required string ChecksumType { get; init; }

    public required bool OldPathIsDir { get; init; }
    public required bool NewPathIsDir { get; init; }
    public required ulong OldPathCount { get; init; }
    public required ulong OldPathDataSize { get; init; }
    public required ulong NewPathCount { get; init; }
    public required ulong NewPathDataSize { get; init; }
    public required ulong OldRefFileCount { get; init; }
    public required ulong OldRefSize { get; init; }          // savedOldRefSize，须等于内嵌 HDIFF13 的 oldDataSize
    public required ulong NewRefFileCount { get; init; }
    public required ulong NewRefSize { get; init; }          // savedNewRefSize，须等于内嵌 HDIFF13 的 newDataSize
    public required ulong SamePairCount { get; init; }       // \"copy from old count\"
    public required ulong CopySize { get; init; }            // samePair 文件总字节数
    public required ulong NewExecuteCount { get; init; }
    public required ulong PrivateReservedDataSize { get; init; }
    public required ulong DiffOffsetAfterDirData { get; init; }  // 生产数据恒为 0
    public required ulong ExtraOffset { get; init; }             // 生产数据恒为 0
    public required ulong DirDataRawSize { get; init; }
    public required ulong DirDataCompSize { get; init; }
    public required ulong ChecksumByteSize { get; init; }

    /// <summary>4 个内嵌校验：[0]=oldRefData [1]=newRefData [2]=copyData(samePair) [3]=diffFile 去掉自身槽位。</summary>
    public required byte[][] Checksums { get; init; }

    /// <summary>校验区起始偏移（19 个 varint 结束处）。</summary>
    public required long ChecksumAreaStart { get; init; }

    /// <summary>dirData 在差分文件中的起始偏移。</summary>
    public required long DirDataStart { get; init; }

    /// <summary>dirData 在差分文件中的字节长度（压缩存在时为压缩长度）。</summary>
    public required long DirDataFileSize { get; init; }

    /// <summary>dirData 结束偏移（内嵌单文件 diff 区域前的边界）。</summary>
    public required long DirDataEnd { get; init; }

    /// <summary>
    /// 内嵌单文件 diff 数据区起点 = DirDataEnd + DiffOffsetAfterDirData + ExtraOffset
    /// （sub_140002970：base = dirDataEnd + v14 + v15；sub_140004530 以 [base, 文件尾) 构造差分数据流）。
    /// </summary>
    public required long SingleDiffStart { get; init; }

    /// <summary>内嵌单文件 diff 数据区终点 = 差分文件尾。</summary>
    public required long SingleDiffEnd { get; init; }

    private const int MaxHeadBytes = 64 * 1024;

    public static Hdiff19Head Parse(string diffFile)
    {
        using var fs = new FileStream(diffFile, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024);
        return Parse(fs, diffFile);
    }

    public static Hdiff19Head Parse(Stream stream, string diffName)
    {
        long fileSize = stream.Length;
        var buf = ArrayPool<byte>.Shared.Rent(MaxHeadBytes);
        try
        {
            int want = (int) Math.Min(buf.Length, fileSize);
            stream.ReadAtLeast(buf, want, true);
            return ParseBuffer(buf, want, fileSize, diffName);
        }
        finally { ArrayPool<byte>.Shared.Return(buf); }
    }
    public static async Task<Hdiff19Head> ParseAsync(string diffFile, CancellationToken ct)
    {
        await using var stream = new FileStream(diffFile, FileMode.Open, FileAccess.Read, FileShare.Read,
            1, FileOptions.Asynchronous);
        var buffer = ArrayPool<byte>.Shared.Rent(MaxHeadBytes);
        try
        {
            int count = (int) Math.Min(MaxHeadBytes, stream.Length);
            await stream.ReadExactlyAsync(buffer.AsMemory(0, count), ct).ConfigureAwait(false);
            return ParseBuffer(buffer, count, stream.Length, diffFile);
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private static Hdiff19Head ParseBuffer(byte[] buf, int want, long fileSize, string diffName)
    {
        var c = new ByteCursor(buf, 0, want);


        if (!c.ReadSpan(8).SequenceEqual("HDIFF19&"u8))
            throw new InvalidDataException($"{diffName}：不是 HDIFF19 目录差分文件。");

        string compressType = c.ReadUntil((byte) '&');
        string checksumType = c.ReadUntil(0);
        if (compressType != "zstd" && compressType != "")
            throw new NotSupportedException(
                $"{diffName}：压缩类型 \"{compressType}\" 不受支持（生产 krpdiff 仅使用 zstd）。");

        Span<ulong> v = stackalloc ulong[19];
        for (int i = 0; i < 19; i++)
            v[i] = c.Unpack(0).Value;

        foreach (ulong value in v)
            if (value > long.MaxValue) throw new InvalidDataException($"{diffName}：header size overflow.");
        ulong byteSize = v[18];
        if (byteSize > 16 || 4 * byteSize > (ulong) c.Remaining)
            throw new InvalidDataException($"{diffName}：checksum size out of range.");
        var checksums = new byte[4][];
        for (int i = 0; i < 4; i++)
            checksums[i] = c.ReadBytes((int) byteSize);

        long checksumAreaStart = c.Position - (int) (4 * byteSize);
        long dirDataStart = c.Position;
        long dirDataFileSize = (long) (v[17] != 0 ? v[17] : v[16]);
        if (dirDataFileSize > fileSize - dirDataStart || v[14] > (ulong) (fileSize - dirDataStart - dirDataFileSize) ||
            v[15] > (ulong) (fileSize - dirDataStart - dirDataFileSize) - v[14])
            throw new InvalidDataException($"{diffName}：directory region out of range.");
        if (compressType == "" && v[17] != 0) throw new InvalidDataException("Missing compression type.");
        long dirDataEnd = dirDataStart + dirDataFileSize;
        long singleDiffStart = dirDataEnd + (long) v[14] + (long) v[15];
        long singleDiffEnd = fileSize;

        if (v[0] > 1 || v[1] > 1)
            throw new InvalidDataException($"{diffName}：路径 isDir 标志越界。");
        if (singleDiffStart >= singleDiffEnd)
            throw new InvalidDataException(
                $"{diffName}：内嵌单文件 diff 区域 [{singleDiffStart},{singleDiffEnd}) 越界（文件 {fileSize} 字节）。");

        return new Hdiff19Head
        {
            CompressType = compressType,
            ChecksumType = checksumType,
            OldPathIsDir = v[0] != 0,
            NewPathIsDir = v[1] != 0,
            OldPathCount = v[2],
            OldPathDataSize = v[3],
            NewPathCount = v[4],
            NewPathDataSize = v[5],
            OldRefFileCount = v[6],
            OldRefSize = v[7],
            NewRefFileCount = v[8],
            NewRefSize = v[9],
            SamePairCount = v[10],
            CopySize = v[11],
            NewExecuteCount = v[12],
            PrivateReservedDataSize = v[13],
            DiffOffsetAfterDirData = v[14],
            ExtraOffset = v[15],
            DirDataRawSize = v[16],
            DirDataCompSize = v[17],
            ChecksumByteSize = byteSize,
            Checksums = checksums,
            ChecksumAreaStart = checksumAreaStart,
            DirDataStart = dirDataStart,
            DirDataFileSize = dirDataFileSize,
            DirDataEnd = dirDataEnd,
            SingleDiffStart = singleDiffStart,
            SingleDiffEnd = singleDiffEnd,
        };
    }

}
