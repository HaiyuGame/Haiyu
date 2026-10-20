using System.Buffers;

namespace Waves.Diff;

internal static class SingleDiffDispatcher
{
    internal readonly record struct Info(bool IsSingleStream, ulong NewSize, ulong OldSize, string Compression);

    public static Info Inspect(Stream diff, long start, long end)
    {
        ValidateRegion(diff, start, end);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(1024);
        try
        {
            diff.Position = start;
            int count = (int) Math.Min(1024, end - start);
            diff.ReadExactly(buffer.AsSpan(0, count));
            return Parse(buffer, count);
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    public static async Task<Info> InspectAsync(Stream diff, long start, long end, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ValidateRegion(diff, start, end);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(1024);
        try
        {
            diff.Position = start;
            int count = (int) Math.Min(1024, end - start);
            await diff.ReadExactlyAsync(buffer.AsMemory(0, count), ct).ConfigureAwait(false);
            return Parse(buffer, count);
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private static void ValidateRegion(Stream diff, long start, long end)
    {
        if (start < 0 || end < start || end > diff.Length)
            throw new InvalidDataException("Single diff region is outside the patch file.");
    }

    private static Info Parse(byte[] buffer, int count)
    {
        var cursor = new ByteCursor(buffer, 0, count);
        string format = cursor.ReadUntil((byte) '&');
        if (format is not ("HDIFF13" or "HDIFFSF20"))
            throw new NotSupportedException($"Single diff format '{format}' is not supported.");
        string compression = cursor.ReadUntil(0);
        ulong newSize = cursor.Unpack(0).Value, oldSize = cursor.Unpack(0).Value;
        if (newSize > long.MaxValue || oldSize > long.MaxValue)
            throw new InvalidDataException("Single diff sizes exceed the stream position range.");
        return new Info(format == "HDIFFSF20", newSize, oldSize, compression);
    }

    public static void Patch(Info info, Stream diff, string name, long start, long end,
        Stream old, Stream output, Action<ulong>? progress, CancellationToken ct)
    {
        if (info.IsSingleStream) HdiffSf20Patcher.Patch(diff, name, start, end, old, output, progress, ct);
        else Hdiff13Patcher.Patch(diff, name, start, end, old, output, progress, ct);
    }

    public static Task PatchAsync(Info info, Stream diff, string name, long start, long end,
        Stream old, Stream output, Action<ulong>? progress, CancellationToken ct) => info.IsSingleStream
            ? HdiffSf20Patcher.PatchAsync(diff, name, start, end, old, output, progress, ct)
            : Hdiff13Patcher.PatchAsync(diff, name, start, end, old, output, progress, ct);
}
