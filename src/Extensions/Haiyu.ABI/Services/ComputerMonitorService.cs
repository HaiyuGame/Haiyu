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
    FpsCounter counter = null;

    public ComputerMonitorService()
    {
        counter = new();
    }

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
        if (counter == null)
            return new(-1, "初始化监控失败");
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
        Task task = new(counter.Start,cancellationToken);
        try
        {
            await task;
            return new(0, "监控结束");
        }
        catch(OperationCanceledException ex)
        {
            // 取消
        }
        catch (Exception)
        {
            // 其他异常
        }
        finally
        {
            counter.Dispose();
        }
        return new(0, "监控结束");
    }
}
