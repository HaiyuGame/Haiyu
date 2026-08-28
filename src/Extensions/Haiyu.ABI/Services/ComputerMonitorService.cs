using ABI.Models;
using ABIRuntime.Abstractions;
using Haiyu.ABI.Common;

namespace Haiyu.ABI.Services;

/// <summary>
/// 硬件监控,初版仅支持FPS
/// </summary>
public sealed partial class ComputerMonitorService
    : IPrivilegedService<CMonitorRequest, RunResult, CMonitorProgress>
{
    public PrivilegedServiceContract<CMonitorRequest, RunResult, CMonitorProgress> Contract =>
        new(
            "haiyu.monitor.v1",
            ABIJsonContext.Default.CMonitorRequest,
            ABIJsonContext.Default.RunResult,
            ABIJsonContext.Default.CMonitorProgress
        );

    public async ValueTask<RunResult> ExecuteAsync(
        CMonitorRequest request,
        IProgress<CMonitorProgress> progress,
        CancellationToken cancellationToken
    )
    {
        using var counter = new FpsCounter();
        counter.FpsOutput = new Action<Tuple<string, int>>(
            (s) =>
            {
                progress.Report(
                    new(
                        new CMonitorProgressData()
                        {
                            ForgroundProgramName = s.Item1,
                            FOrgroundProgramFps = s.Item2,
                        }
                    )
                );
            }
        );
        counter.Start();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return new(0, "监控结束");
    }
}
