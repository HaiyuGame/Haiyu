using ABI.Models;
using ABIRuntime;
using ABIRuntime.Abstractions;
using Haiyu.ABI.XBox.Commons;
using Microsoft.Extensions.DependencyInjection;

namespace Haiyu.ABI.Services;

public static class BuiltInServices
{
    public static IServiceCollection AddBuiltInServices(this IServiceCollection services)
    {
        services.AddPrivilegedService<
            CleanMemoryRequest,
            CleanMemoryProgress,
            RunResult,
            MemoryCleanerService
        >(Contract.CleanMemoryContract);

        services.AddPrivilegedService<
            CMonitorRequest,
            CMonitorProgress,
            RunResult,
            ComputerMonitorService
        >(Contract.ComputerMonitorContract);

        services.AddPrivilegedService<
            FpsMonitorRequest,
            FpsMonitorProgress,
            RunResult,
            FpsMonitorService
        >(Contract.FpsMonitorContract);

        services.AddPrivilegedService<
            ABISystemConfigRequest,
            ABISystemConfigProgress,
            RunResult,
            ABISystemConfigService
        >(Contract.ABISystemConfigContract);

        // XBoxService 是 XboxOpen/CloseControllerService 的构造依赖，必须先注册。
        // XBoxController 是 XBoxService 的构造依赖（Haiyu.ABI.XBox.Commons）。
        // 注册收敛到 AddBuiltInServices：ElevatedEntry 与 MonitorSelfTest 两个宿主入口
        // 共用同一份注册，避免入口间不一致导致 DI 验证失败。
        services.AddSingleton<XBoxController>();
        services.AddSingleton<XBoxService>();

        services.AddPrivilegedService<
            XboxExtOpenRequest,
            XboxReportProcess,
            RunResult,
            XboxOpenControllerService
        >(Contract.XboxOpenControllerContract);

        services.AddPrivilegedService<
            XboxExtCloseRequest,
            XboxReportProcess,
            RunResult,
            XboxCloseControllerService
        >(Contract.XboxCloseControllerContract);
        services.AddSingleton<IXboxSessionSource, XInputSessionSource>();
        services.AddPrivilegedService<
            XboxSessionRequest, XboxSessionReportProcess, RunResult, XboxSessionService
        >(Contract.XboxSessionContract);
        return services;
    }
}
