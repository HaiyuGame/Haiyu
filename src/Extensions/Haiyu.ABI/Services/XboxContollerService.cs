using ABI.Models;
using ABIRuntime.Abstractions;

namespace Haiyu.ABI.Services;

public class XboxOpenControllerService
    : IPrivilegedService<XboxExtOpenRequest, XboxReportProcess, RunResult>
{
    public XboxOpenControllerService(XBoxService xBoxService)
    {
        XBoxService = xBoxService;
    }

    public PrivilegedServiceContract<XboxExtOpenRequest, XboxReportProcess, RunResult> Contract =>
        ABIRuntime.Contract.XboxOpenControllerContract;

    public XBoxService XBoxService { get; }

    public async ValueTask<RunResult> ExecuteAsync(
        XboxExtOpenRequest request,
        IProgress<XboxReportProcess> progress,
        CancellationToken cancellationToken
    )
    {
        return await XBoxService.SetModelAsync(true);
    }
}

public class XboxCloseControllerService
    : IPrivilegedService<XboxExtCloseRequest, XboxReportProcess, RunResult>
{
    public XboxCloseControllerService(XBoxService xBoxService)
    {
        XBoxService = xBoxService;
    }

    public PrivilegedServiceContract<XboxExtCloseRequest, XboxReportProcess, RunResult> Contract =>
        ABIRuntime.Contract.XboxCloseControllerContract;

    public XBoxService XBoxService { get; }

    public async ValueTask<RunResult> ExecuteAsync(
        XboxExtCloseRequest request,
        IProgress<XboxReportProcess> progress,
        CancellationToken cancellationToken
    )
    {
        return await XBoxService.SetModelAsync(false);
    }
}
