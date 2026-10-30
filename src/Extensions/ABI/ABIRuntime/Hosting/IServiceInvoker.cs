using ABI.Models;

namespace ABIRuntime.Runtime;

internal interface IServiceInvoker
{
    ValueTask InvokeAsync(Stream pipe, PipeMessage request, CancellationToken cancellationToken);
}
