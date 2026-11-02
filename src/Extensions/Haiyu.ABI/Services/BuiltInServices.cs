using ABI.Models;
using ABIRuntime;
using ABIRuntime.Abstractions;
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
        return services;
    }
}
