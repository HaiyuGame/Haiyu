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
        ABISystemConfigProgress,
        RunResult
    > ABISystemConfigContract =>
        new PrivilegedServiceContract<ABISystemConfigRequest, ABISystemConfigProgress, RunResult>(
            "haiyu.systemInit.v1",
            ABIJsonContext.Default.ABISystemConfigRequest,
            ABIJsonContext.Default.ABISystemConfigProgress,
            ABIJsonContext.Default.RunResult
        );

    public static PrivilegedServiceContract<
        CleanMemoryRequest,
        CleanMemoryProgress,
        RunResult
    > CleanMemoryContract =>
        new(
            "haiyu.clean.v1",
            ABIJsonContext.Default.CleanMemoryRequest,
            ABIJsonContext.Default.CleanMemoryProgress,
            ABIJsonContext.Default.RunResult
        );

    public static PrivilegedServiceContract<
        CMonitorRequest,
        CMonitorProgress,
        RunResult
    > ComputerMonitorContract =>
        new(
            "haiyu.monitor.v1",
            ABIJsonContext.Default.CMonitorRequest,
            ABIJsonContext.Default.CMonitorProgress,
            ABIJsonContext.Default.RunResult
        );

    public static PrivilegedServiceContract<
        FpsMonitorRequest,
        FpsMonitorProgress,
        RunResult
    > FpsMonitorContract =>
        new(
            "haiyu.fpsMonitor.v1",
            ABIJsonContext.Default.FpsMonitorRequest,
            ABIJsonContext.Default.FpsMonitorProgress,
            ABIJsonContext.Default.RunResult
        );

    public static PrivilegedServiceContract<
        XboxExtOpenRequest,
        XboxReportProcess,
        RunResult
    > XboxOpenControllerContract =>
        new(
            "haiyu.xboxOpen.v1",
            ABIJsonContext.Default.XboxExtOpenRequest,
            ABIJsonContext.Default.XboxReportProcess,
            ABIJsonContext.Default.RunResult
        );

    public static PrivilegedServiceContract<
        XboxExtCloseRequest,
        XboxReportProcess,
        RunResult
    > XboxCloseControllerContract =>
        new(
            "haiyu.xboxClose.v1",
            ABIJsonContext.Default.XboxExtCloseRequest,
            ABIJsonContext.Default.XboxReportProcess,
            ABIJsonContext.Default.RunResult
        );
    /// <summary>单次采集四个 XInput 槽位，通过进度返回快照。</summary>
    public static PrivilegedServiceContract<XboxSessionRequest, XboxSessionReportProcess, RunResult>
        XboxSessionContract => new(
            "haiyu.xboxSession.v1",
            ABIJsonContext.Default.XboxSessionRequest,
            ABIJsonContext.Default.XboxSessionReportProcess,
            ABIJsonContext.Default.RunResult);
}
