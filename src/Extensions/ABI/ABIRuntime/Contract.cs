using System;
using System.Collections.Generic;
using System.Text;
using ABI.Models;
using ABIRuntime.Abstractions;

namespace ABIRuntime;

public static class Contract
{
    public static PrivilegedServiceContract<
        ABISystemConfigRequest,
        RunResult,
        ABISystemConfigProgress
    > ABISystemConfigContract =>
        new PrivilegedServiceContract<
            ABISystemConfigRequest,
            RunResult,
            ABISystemConfigProgress
        >(
            "haiyu.systemInit.v1",
            ABIJsonContext.Default.ABISystemConfigRequest,
            ABIJsonContext.Default.RunResult,
            ABIJsonContext.Default.ABISystemConfigProgress
        );

    public static PrivilegedServiceContract<
        CleanMemoryRequest,
        RunResult,
        CleanMemoryProgress
    > CleanMemoryContract =>
        new(
            "haiyu.clean.v1",
            ABIJsonContext.Default.CleanMemoryRequest,
            ABIJsonContext.Default.RunResult,
            ABIJsonContext.Default.CleanMemoryProgress
        );

    public static PrivilegedServiceContract<
        CMonitorRequest,
        RunResult,
        CMonitorProgress
    > ComputerMonitorContract =>
        new(
            "haiyu.monitor.v1",
            ABIJsonContext.Default.CMonitorRequest,
            ABIJsonContext.Default.RunResult,
            ABIJsonContext.Default.CMonitorProgress
        );

    public static PrivilegedServiceContract<
        FpsMonitorRequest,
        RunResult,
        FpsMonitorProgress
    > FpsMonitorContract =>
        new(
            "haiyu.fpsMonitor.v1",
            ABIJsonContext.Default.FpsMonitorRequest,
            ABIJsonContext.Default.RunResult,
            ABIJsonContext.Default.FpsMonitorProgress
        );
}
