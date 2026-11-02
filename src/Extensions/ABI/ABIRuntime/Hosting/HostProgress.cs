using ABI.Models;
using MemoryPack;

namespace ABIRuntime.Runtime;

internal sealed class HostProgress<TProgress>(Stream pipe, PipeMessage request)
    : IProgress<TProgress>
{
    private readonly object _sync = new();

    public void Report(TProgress value)
    {
        lock (_sync)
        {
            byte[] payload = MemoryPackSerializer.Serialize(value, ABIMemoryPack.Options);
            PipeProtocol
                .WriteAsync(
                    pipe,
                    new PipeMessage(
                        PipeMessageKind.Progress,
                        request.RequestId,
                        request.Version,
                        request.Operation,
                        payload,
                        0,
                        "业务进度"
                    ),
                    CancellationToken.None
                )
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
    }
}
