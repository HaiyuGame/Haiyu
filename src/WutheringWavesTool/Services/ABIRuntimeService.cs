using ABI.Models;
using ABIRuntime.Abstractions;
using ABIRuntime.Runtime;
using Microsoft.Extensions.Hosting;

namespace Haiyu.Services;

public class ABIRuntimeService : IHostedService
{
    PrivilegedRuntime? _runtime;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _runtime = new("Haiyu.ABI.dll");
        var progress = new Progress<IPrivilegedProgress<CleanMemoryProgress>>(progress =>
        {
            Console.WriteLine(
                $"Stage: {progress.Stage}, Progress: {progress.Percentage}, Message: {progress.Message}"
            );
        });
        var task = await _runtime.InvokeAsync<CleanMemoryRequest, RunResult, CleanMemoryProgress>(
            new(
                "haiyu.clean.v1",
                ABIJsonContext.Default.CleanMemoryRequest,
                ABIJsonContext.Default.RunResult,
                ABIJsonContext.Default.CleanMemoryProgresss
            ),
            new CleanMemoryRequest(""),
            progress,
            cancellationToken
        );
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_runtime != null)
        {
            await _runtime.DisposeAsync();
            _runtime = null;
        }
    }
}
