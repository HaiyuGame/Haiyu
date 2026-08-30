using System.Text.Json.Serialization;
using MemoryPack;

namespace ABI.Models;

/*
 此文件中存放ABI交换数据模型，Haiyu与Haiyu.ABI双向通信时的数据包装
 
 */

#region Cleaner


/// <summary>
/// 清理内存请求，programNames为要清理的程序名，多个程序名用逗号分隔
/// </summary>
/// <param name="programNames"></param>
[MemoryPackable]
public sealed partial record CleanMemoryRequest(string programNames);

/// <summary>
/// 清理内存进度
/// </summary>
/// <param name="Percentage"></param>
/// <param name="Message"></param>
[MemoryPackable]
public sealed partial record CleanMemoryProgress(int Percentage, string Message);
#endregion

#region Hardware monitor

/// <summary>
/// 系统硬件监控
/// </summary>
[MemoryPackable]
public sealed partial record CMonitorRequest();

[MemoryPackable]
public sealed partial record CMonitorProgress(
    [property: JsonPropertyName("data")] MonitorRecord data
);

#endregion

#region FPS monitor

[MemoryPackable]
public sealed partial record FpsMonitorRequest();

[MemoryPackable]
public sealed partial record FpsMonitorProgress(
    [property: JsonPropertyName("data")] FPSData data
);

#endregion
/// <summary>
/// 运行结果
/// </summary>
/// <param name="code"></param>
/// <param name="msg"></param>
[MemoryPackable]
public sealed partial record RunResult(int code, string msg);

public enum PipeMessageKind
{
    Request,
    Progress,
    Result,
    Error,
    Cancel,
    Cancelled,
}

[MemoryPackable]
public sealed partial record PipeMessage(
    PipeMessageKind Kind,
    Guid RequestId,
    int Version,
    string Operation,
    byte[] Payload,
    int Percentage = 0,
    string Message = "",
    int StatusCode = 0
);

/// <summary>控制管道用于创建独立请求管道的连接信息。</summary>
[MemoryPackable]
public sealed partial record OpenRequestMessage(string PipeName, string Secret);
