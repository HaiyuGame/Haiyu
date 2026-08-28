using System.Text.Json;
using ABI.Models;

namespace ABIRuntime.Runtime;

internal static class OperationNames
{
    internal const string Handshake = "system.handshake.v1";
}

internal static class PipeProtocolVersion
{
    internal const int Current = 1;
}

internal static class PipeProtocol
{
    private const int MaxMessageLength = 4 * 1024 * 1024;

    internal static async ValueTask WriteAsync(Stream stream, PipeMessage message,
        CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            message, PipeJsonContext.Default.PipeMessage);
        await stream.WriteAsync(BitConverter.GetBytes(payload.Length), cancellationToken)
            .ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<PipeMessage> ReadAsync(Stream stream,
        CancellationToken cancellationToken)
    {
        byte[] lengthBytes = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, lengthBytes, cancellationToken).ConfigureAwait(false);
        int length = BitConverter.ToInt32(lengthBytes);
        if (length <= 0 || length > MaxMessageLength)
            throw new InvalidDataException("管道消息长度无效。");

        byte[] payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(payload, PipeJsonContext.Default.PipeMessage)
            ?? throw new InvalidDataException("管道消息为空。");
    }

    private static async ValueTask ReadExactlyAsync(Stream stream, Memory<byte> buffer,
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
