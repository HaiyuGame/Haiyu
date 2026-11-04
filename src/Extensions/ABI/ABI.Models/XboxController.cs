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
/// Xbox获取信息
/// </summary>
[MemoryPackable]
public sealed partial record XboxSessionRequest();

[MemoryPackable]
public sealed partial record XboxSessionReportProcess(XboxSession[] Controllers);

[MemoryPackable]
public sealed partial class XboxSession
{
    /// <summary>XInput 槽位，0～3；不是持久设备标识。</summary>
    public int UserIndex { get; set; }
    public bool IsConnected { get; set; }

    /// <summary>XInput 状态查询错误码；非零时 IsConnected 不代表确定的断开状态。</summary>
    public uint StateErrorCode { get; set; }
    public XboxConnectionType ConnectionType { get; set; }
    public XboxBatteryType BatteryType { get; set; }
    public XboxBatteryLevel BatteryLevel { get; set; }

    /// <summary>预留百分比，XInput 只提供档位，因此为 null。</summary>
    public double? Power { get; set; }
}

/// <summary>
/// Xbox扩展运行回报消息，目前定义空
/// </summary>
/// <param name="configFile">配置文件地址</param>
[MemoryPackable]
public sealed partial record XboxReportProcess(string message);

/// <summary>Wireless 不区分蓝牙和 Xbox 无线适配器。</summary>
public enum XboxConnectionType
{
    Unknown,
    Wired,
    Wireless,
}

public enum XboxBatteryType
{
    Unknown,
    Disconnected,
    Wired,
    Alkaline,
    NickelMetalHydride,
}

public enum XboxBatteryLevel
{
    Unknown,
    Empty,
    Low,
    Medium,
    Full,
}
