using MemoryPack;

namespace ABI.Models;

/// <summary>
/// Xbox扩展开启命令
/// </summary>
/// <param name="configFile">配置文件地址</param>
[MemoryPackable]
public sealed partial record XboxExtOpenRequest(string configFile);


/// <summary>
/// Xbox扩展关闭命令
/// </summary>
/// <param name="configFile">配置文件地址</param>
[MemoryPackable]
public sealed partial record XboxExtCloseRequest();


/// <summary>
/// Xbox扩展运行回报消息，目前定义空
/// </summary>
/// <param name="configFile">配置文件地址</param>
[MemoryPackable]
public sealed partial record XboxReportProcess(string message);
