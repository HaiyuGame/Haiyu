using System.Diagnostics;
using ABI.Models;
using ABIRuntime.Abstractions;
using LibreHardwareMonitor.PawnIo;

namespace Haiyu.ABI.Services;

public sealed class ABISystemConfigService
    : IPrivilegedService<ABISystemConfigRequest, RunResult, ABISystemConfigProgress>
{
    private string path;

    public PrivilegedServiceContract<
        ABISystemConfigRequest,
        RunResult,
        ABISystemConfigProgress
    > Contract => ABIRuntime.Contract.ABISystemConfigContract;

    public async ValueTask<RunResult> ExecuteAsync(
        ABISystemConfigRequest request,
        IProgress<ABISystemConfigProgress> progress,
        CancellationToken cancellationToken
    )
    {
        path = request.BaseDirectory;
        progress.Report(new() { IsComplete = false, IsRuning = true });
        if (PawnIo.IsInstalled)
        {
            if(PawnIo.Version<new Version(2, 0, 0, 0))
            {
                await InstallPawnIOAsync(cancellationToken);
            }
        }
        else
        {
            await InstallPawnIOAsync(cancellationToken);
        }
        return new RunResult(0, "成功");
    }


    async Task InstallPawnIOAsync(CancellationToken token =default)
    {
        var installPath = Path.Combine(path,"Haiyu.ABI","Assets", "PawnIOSetup.exe");
        if (!string.IsNullOrEmpty(path))
        {
            var process = Process.Start(new ProcessStartInfo(installPath, "-install"));
            if (process == null)
                return;
            await process.WaitForExitAsync();
        }
    }
}
