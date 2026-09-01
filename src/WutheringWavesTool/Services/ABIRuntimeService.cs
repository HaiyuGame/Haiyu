using ABI.Models;
using ABIRuntime.Abstractions;
using ABIRuntime.Runtime;
using Haiyu.Plugin.Common.LegacyMessageBox;
using Microsoft.Extensions.Hosting;

namespace Haiyu.Services;

public sealed class ABIRuntimeService
{
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private bool _initialized;

    public PrivilegedRuntime? Runtime { get; private set; }

    /// <summary>
    /// 初始化后台ABI运行时
    /// </summary>
    public async Task<bool> Initialize(string baseDirectory)
    {
        await _initializeGate.WaitAsync();
        try
        {
            if (_initialized && Runtime is not null)
                return true;

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
                    Debug.WriteLine($"Run{configProgress.IsRuning}");
                }
            });
            var result = await this.Runtime.InvokeAsync(
                ABIRuntime.Contract.ABISystemConfigContract,
                new ABISystemConfigRequest() { BaseDirectory = baseDirectory },
                progress,
                default
            );
            _initialized = result.IsSuccess;
            return _initialized;
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
        finally
        {
            _initializeGate.Release();
        }
    }

    public async Task Close()
    {
        if (Runtime == null)
            return;
        await Runtime.DisposeAsync();
        Runtime = null;
        _initialized = false;
    }
}
