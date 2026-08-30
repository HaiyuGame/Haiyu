using ABI.Models;
using ABIRuntime.Abstractions;
using Haiyu.ABI.Common;

namespace Haiyu.ABI.Services;

/// <summary>
/// 硬件监控
/// </summary>
public sealed partial class ComputerMonitorService
    : IPrivilegedService<CMonitorRequest, RunResult, CMonitorProgress>
{
    public PrivilegedServiceContract<CMonitorRequest, RunResult, CMonitorProgress> Contract =>
        ABIRuntime.Contract.ComputerMonitorContract;

    public async ValueTask<RunResult> ExecuteAsync(
        CMonitorRequest request,
        IProgress<CMonitorProgress> progress,
        CancellationToken cancellationToken
    )
    {
        using var monitorCounter = new MonitorCounter();
        monitorCounter.MonitorOutput = data =>
            progress.Report(new CMonitorProgress(data));
        await monitorCounter.RunAsync(cancellationToken).ConfigureAwait(false);
        return new(0, "监控结束");
    }
}
