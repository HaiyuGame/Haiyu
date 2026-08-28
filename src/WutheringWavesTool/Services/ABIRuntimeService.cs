using ABI.Models;
using ABIRuntime.Abstractions;
using ABIRuntime.Runtime;
using Microsoft.Extensions.Hosting;

namespace Haiyu.Services;

public sealed class ABIRuntimeService : IHostedService
{
    private PrivilegedRuntime? _runtime;
    private CancellationTokenSource? _monitorCancellation;
    private Task? _monitorTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        string corePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Haiyu.ABI",
            "Haiyu.ABI.dll"
        );

        _runtime = new PrivilegedRuntime(corePath);
        _monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var progress = new Progress<IPrivilegedProgress<CMonitorProgress>>(value =>
        {
            if (value.Data?.data is not { } data)
                return;

            Debug.WriteLine($"{data.ForgroundProgramName},{data.FOrgroundProgramFps}");
        });

        _monitorTask = MonitorAsync(progress, _monitorCancellation.Token);
        return Task.CompletedTask;
    }

    private async Task MonitorAsync(
        IProgress<IPrivilegedProgress<CMonitorProgress>> progress,
        CancellationToken cancellationToken
    )
    {
        try
        {
            IPrivilegedResult<RunResult> result = await _runtime!.InvokeAsync<
                CMonitorRequest,
                RunResult,
                CMonitorProgress
            >(
                new(
                    "haiyu.monitor.v1",
                    ABIJsonContext.Default.CMonitorRequest,
                    ABIJsonContext.Default.RunResult,
                    ABIJsonContext.Default.CMonitorProgress
                ),
                new CMonitorRequest(),
                progress,
                cancellationToken
            );

            if (!result.IsSuccess)
            {
                Debug.WriteLine($"FPS 监控失败：0x{result.StatusCode:X8} {result.Message}");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Debug.WriteLine("FPS 监控已取消。");
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"FPS 监控异常：{exception}");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_monitorCancellation is not null)
            await _monitorCancellation.CancelAsync();

        if (_monitorTask is not null)
        {
            try
            {
                await _monitorTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        }

        if (_runtime is not null)
        {
            await _runtime.DisposeAsync();
            _runtime = null;
        }

        _monitorCancellation?.Dispose();
        _monitorCancellation = null;
        _monitorTask = null;
    }
}
