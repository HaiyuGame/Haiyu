using ABI.Models;
using ABIRuntime.Abstractions;
using ABIRuntime.Runtime;
using Haiyu.Plugin.Common.LegacyMessageBox;
using Microsoft.Extensions.Hosting;

namespace Haiyu.Services;

public sealed class ABIRuntimeService
{
    public PrivilegedRuntime? Runtime { get; private set; }

    /// <summary>
    /// 初始化后台ABI运行时
    /// </summary>
    public async Task<bool> Initialize(string baseDirectory)
    {
        string corePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Haiyu.ABI",
            "Haiyu.ABI.dll"
        );
        try
        {
            if (Runtime == null || Runtime.RunFlage != 0)
            {
                Runtime = new PrivilegedRuntime(corePath);
            }
            Progress<IPrivilegedProgress<ABISystemConfigProgress>> progress = new Progress<
                IPrivilegedProgress<ABISystemConfigProgress>
            >(p =>
            {
                if (p.Data is { } configProgress)
                {
                    Console.WriteLine($"Run{configProgress.IsRuning}");
                }
            });
            var result = await this.Runtime.InvokeAsync(
                ABIRuntime.Contract.ABISystemConfigContract,
                new ABISystemConfigRequest() { BaseDirectory = baseDirectory },
                progress,
                default
            );
            return result.IsSuccess;
        }
        catch (Exception)
        {
            LegacyMessageBox.ShowError(
                "初始化后台ABI运行时失败，请确保 Haiyu.ABI.dll 存在于 Haiyu.ABI 文件夹中。",
                "错误"
            );
            return false;
        }
    }

    public async Task Close()
    {
        if (Runtime == null)
            return;
        await Runtime.DisposeAsync();
    }
}
