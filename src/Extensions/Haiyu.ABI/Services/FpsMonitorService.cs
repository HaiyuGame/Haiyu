using ABI.Models;
using ABIRuntime.Abstractions;
using Haiyu.ABI.Common;

namespace Haiyu.ABI.Services;

/// <summary>ETW FPS 监控服务。</summary>
public sealed class FpsMonitorService
    : IPrivilegedService<FpsMonitorRequest, RunResult, FpsMonitorProgress>
{
    public PrivilegedServiceContract<FpsMonitorRequest, RunResult, FpsMonitorProgress> Contract =>
        ABIRuntime.Contract.FpsMonitorContract;

    public async ValueTask<RunResult> ExecuteAsync(
        FpsMonitorRequest request,
        IProgress<FpsMonitorProgress> progress,
        CancellationToken cancellationToken)
    {
        await using var fpsCounter = new FpsCounter();
        fpsCounter.FpsOutput = value =>
            progress.Report(new FpsMonitorProgress(value));

        await fpsCounter.StartAsync(cancellationToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return new RunResult(0, "FPS 监控结束");
    }
}
