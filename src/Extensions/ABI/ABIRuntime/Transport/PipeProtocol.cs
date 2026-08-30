using ABI.Models;
using MemoryPack;

namespace ABIRuntime.Runtime;

internal static class OperationNames
{
    internal const string Handshake = "system.handshake.v1";
    internal const string OpenRequest = "system.open-request.v1";
    internal const string RequestHandshake = "system.request-handshake.v1";
}

internal static class PipeProtocolVersion
{
    internal const int Current = 2;
}

/// <summary>MemoryPack 二进制长度前缀协议；ReadExactly 保证系统拆包后完整还原。</summary>
internal static class PipeProtocol
{
    internal const int MaxMessageLength = 64 * 1024 * 1024;

    internal static async ValueTask WriteAsync(
        Stream stream,
        PipeMessage message,
        CancellationToken cancellationToken)
    {
        ABIMemoryPack.EnsureFormatters();
        byte[] payload = MemoryPackSerializer.Serialize(message, ABIMemoryPack.Options);

        if (payload.Length > MaxMessageLength)
            throw new InvalidDataException(
                $"管道消息超过 {MaxMessageLength / 1024 / 1024} MiB 限制：{payload.Length} 字节。");

        byte[] lengthPrefix = BitConverter.GetBytes(payload.Length);
        await stream.WriteAsync(lengthPrefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<PipeMessage> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        byte[] lengthBytes = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, lengthBytes, cancellationToken).ConfigureAwait(false);

        int length = BitConverter.ToInt32(lengthBytes);
        if (length <= 0 || length > MaxMessageLength)
            throw new InvalidDataException($"管道消息长度无效：{length} 字节。");

        byte[] payload = GC.AllocateUninitializedArray<byte>(length);
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);

        ABIMemoryPack.EnsureFormatters();
        return MemoryPackSerializer.Deserialize<PipeMessage>(payload, ABIMemoryPack.Options)
            ?? throw new InvalidDataException("管道消息为空。");
    }

    private static async ValueTask ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer[offset..], cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("命名管道连接已关闭。");
            offset += read;
        }
    }
}
