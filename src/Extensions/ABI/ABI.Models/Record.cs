using System.Text.Json.Serialization;

namespace ABI.Models;

/*
 此文件中存放ABI交换数据模型，Haiyu与Haiyu.ABI双向通信时的数据包装
 
 */

#region Cleaner


/// <summary>
/// 清理内存请求，programNames为要清理的程序名，多个程序名用逗号分隔
/// </summary>
/// <param name="programNames"></param>
public sealed record CleanMemoryRequest(string programNames);

/// <summary>
/// 清理内存进度
/// </summary>
/// <param name="Percentage"></param>
/// <param name="Message"></param>
public sealed record CleanMemoryProgress(int Percentage, string Message);
#endregion

#region FPS

/// <summary>
/// FPS 监控
/// </summary>
public sealed record CMonitorRequest();

public sealed class CMonitorProgressData
{
    [JsonPropertyName("forgroundProgramName")]
    public string ForgroundProgramName { get; set; }

    [JsonPropertyName("forgroudProgramFps")]
    public int FOrgroundProgramFps { get; set; }
}

/// <summary>
/// FPS上报
/// </summary>
/// <param name="programName"></param>
/// <param name="fpsCount"></param>
public sealed record CMonitorProgress(
    [property: JsonPropertyName("data")] CMonitorProgressData data
);

#endregion
/// <summary>
/// 运行结果
/// </summary>
/// <param name="code"></param>
/// <param name="msg"></param>
public sealed record RunResult(int code, string msg);

public enum PipeMessageKind
{
    Request,
    Progress,
    Result,
    Error,
    Cancel,
    Cancelled,
}

public sealed record PipeMessage(
    PipeMessageKind Kind,
    Guid RequestId,
    int Version,
    string Operation,
    string Payload,
    int Percentage = 0,
    string Message = "",
    int StatusCode = 0
);
