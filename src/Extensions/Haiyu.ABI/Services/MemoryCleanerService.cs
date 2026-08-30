using ABI.Models;
using ABIRuntime.Abstractions;

namespace Haiyu.ABI.Services;

/// <summary>
/// 测试
/// </summary>
public class MemoryCleanerService
    : IPrivilegedService<CleanMemoryRequest, RunResult, CleanMemoryProgress>
{
    public PrivilegedServiceContract<CleanMemoryRequest, RunResult, CleanMemoryProgress> Contract =>
        ABIRuntime.Contract.CleanMemoryContract;

    public async ValueTask<RunResult> ExecuteAsync(
        CleanMemoryRequest request,
        IProgress<CleanMemoryProgress> progress,
        CancellationToken cancellationToken
    )
    {
        return await Task.FromResult(new RunResult(0, "测试成功"));
    }
}
