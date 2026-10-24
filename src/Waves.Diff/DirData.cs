using System.Text;

namespace Waves.Diff;

/// <summary>
/// HDIFF19 dirData 解析（hpatchz.exe sub_1400036D0 TDirPatcher_loadDirData 逆向语义）。
/// 布局：路径串块（'/'→'\\'，NUL 分隔，旧块+新块）+ 7 段列表 + privateReserved 尾部对账。
/// </summary>
internal sealed class DirData
{
    /// <summary>旧路径表（含目录前缀节点，索引供 oldRefList/samePair.oldIndex 引用）。</summary>
    public required string[] OldPaths { get; init; }
    public required string[] NewPaths { get; init; }

    /// <summary>增量索引列表（cur += v + 1），值域 [0, OldPathCount)。</summary>
    public required ulong[] OldRefList { get; init; }
    public required ulong[] NewRefList { get; init; }
    public required ulong[] OldRefSizeList { get; init; }
    public required ulong[] NewRefSizeList { get; init; }
    /// <summary>每个新引用文件的整文件校验值（fadler64）。</summary>
    public required ulong[] NewRefHashList { get; init; }
    /// <summary>成对 (newIndex, oldIndex)：新文件与旧文件内容相同，直接拷贝。</summary>
    public required (ulong NewIndex, ulong OldIndex)[] SamePairs { get; init; }
    /// <summary>需要可执行位的新路径索引（增量列表）。</summary>
    public required ulong[] NewExecuteList { get; init; }

    public static DirData Parse(byte[] dirData, Hdiff19Head head, string diffName)
    {
        var c = new ByteCursor(dirData, 0, dirData.Length);
        if ((ulong) dirData.Length != head.DirDataRawSize)
            throw new InvalidDataException($"{diffName}：dirData 解压后长度不符。");

        // Decode each block directly into its final array (no cloned path block or combined array).
        static int Count(ulong value, long remaining, string field)
        {
            if (value > int.MaxValue || value > (ulong) remaining)
                throw new InvalidDataException($"dirData {field} 数量超出数据边界。");
            return (int) value;
        }

        string[] ReadPaths(ulong byteSize, ulong count)
        {
            int size = Count(byteSize, c.Remaining, "pathData");
            int length = Count(count, size, "paths");
            var block = c.ReadSpan(size);
            var result = new string[length];
            int position = 0;
            for (int i = 0; i < length; i++)
            {
                int end = block[position..].IndexOf((byte) 0);
                if (end < 0) throw new InvalidDataException($"{diffName}：dirData 路径串未 NUL 结尾。");
                string path = string.Create(Encoding.UTF8.GetCharCount(block.Slice(position, end)),
                    (Buffer: dirData, Offset: c.Position - size + position, Length: end),
                    static (chars, source) =>
                    {
                        Encoding.UTF8.GetChars(source.Buffer.AsSpan(source.Offset, source.Length), chars);
                        foreach (ref char value in chars)
                            if (value is '/' or '\\') value = Path.DirectorySeparatorChar;
                    });
                if (Path.IsPathRooted(path) || path.Contains(':'))
                    throw new InvalidDataException($"{diffName}：dirData 路径必须为相对路径。");
                foreach (var component in path.AsSpan().Split(Path.DirectorySeparatorChar))
                    if (path.AsSpan()[component].SequenceEqual(".."))
                        throw new InvalidDataException($"{diffName}：dirData 路径包含上级目录。");
                result[i] = path;
                position += end + 1;
            }
            if (position != block.Length) throw new InvalidDataException($"{diffName}：dirData 路径块存在多余字节。");
            return result;
        }

        var oldPaths = ReadPaths(head.OldPathDataSize, head.OldPathCount);
        var newPaths = ReadPaths(head.NewPathDataSize, head.NewPathCount);

        ulong[] ReadIncList(ulong count, ulong bound, string field)
        {
            if (count > bound) throw new InvalidDataException($"{diffName}：dirData {field} 数量超出路径数量。");
            var r = new ulong[Count(count, c.Remaining, "list")];
            ulong cur = ulong.MaxValue;
            for (ulong i = 0; i < count; i++)
            {
                ulong delta = c.Unpack(0).Value;
                if (i == 0) cur = delta;
                else
                {
                    if (delta == ulong.MaxValue || cur > ulong.MaxValue - delta - 1)
                        throw new InvalidDataException($"{diffName}：dirData {field} 索引溢出。");
                    cur += delta + 1;
                }
                if (cur >= bound)
                    throw new InvalidDataException($"{diffName}：dirData {field} 值 {cur} 越界（上界 {bound}）。");
                r[i] = cur;
            }
            return r;
        }

        ulong[] ReadList(ulong count)
        {
            var r = new ulong[Count(count, c.Remaining, "list")];
            for (ulong i = 0; i < count; i++)
                r[i] = c.Unpack(0).Value;
            return r;
        }

        var oldRefList = ReadIncList(head.OldRefFileCount, head.OldPathCount, "oldRefList");
        var newRefList = ReadIncList(head.NewRefFileCount, head.NewPathCount, "newRefList");
        var oldRefSizes = ReadList(head.OldRefFileCount);
        var newRefSizes = ReadList(head.NewRefFileCount);
        var newRefHashes = ReadList(head.NewRefFileCount);

        if (head.SamePairCount > head.NewPathCount)
            throw new InvalidDataException($"{diffName}：dirData samePairs 数量超出新路径数量。");
        var samePairs = new (ulong, ulong)[Count(head.SamePairCount, c.Remaining / 2, "samePairs")];
        {
            ulong newCur = ulong.MaxValue;
            long oldCur = -1;
            for (ulong i = 0; i < head.SamePairCount; i++)
            {
                ulong delta = c.Unpack(0).Value;
                if (i == 0) newCur = delta;
                else
                {
                    if (delta == ulong.MaxValue || newCur > ulong.MaxValue - delta - 1)
                        throw new InvalidDataException($"{diffName}：dirData samePair newIndex 溢出。");
                    newCur += delta + 1;
                }
                if (newCur >= head.NewPathCount)
                    throw new InvalidDataException($"{diffName}：dirData samePair newIndex 越界。");
                // oldIndex：首字节 tag 位（bit7）决定方向，cur = cur ± inc + 1（sub_1400030C0）
                var (inc, tag) = c.Unpack(1);
                if (inc > long.MaxValue || (tag == 0 && oldCur >= 0 && inc > (ulong) (long.MaxValue - oldCur - 1)))
                    throw new InvalidDataException($"{diffName}：dirData samePair oldIndex 溢出。");
                oldCur = (tag != 0 ? oldCur - (long) inc : oldCur + (long) inc) + 1;
                if (oldCur < 0 || (ulong) oldCur >= head.OldPathCount)
                    throw new InvalidDataException($"{diffName}：dirData samePair oldIndex 越界。");
                samePairs[i] = (newCur, (ulong) oldCur);
            }
        }

        var newExecuteList = ReadIncList(head.NewExecuteCount, head.NewPathCount, "newExecuteList");

        if ((ulong) c.Remaining != head.PrivateReservedDataSize)
            throw new InvalidDataException(
                $"{diffName}：dirData 尾部长度 {c.Remaining} != privateReservedDataSize {head.PrivateReservedDataSize}。");

        return new DirData
        {
            OldPaths = oldPaths,
            NewPaths = newPaths,
            OldRefList = oldRefList,
            NewRefList = newRefList,
            OldRefSizeList = oldRefSizes,
            NewRefSizeList = newRefSizes,
            NewRefHashList = newRefHashes,
            SamePairs = samePairs,
            NewExecuteList = newExecuteList,
        };
    }
}
