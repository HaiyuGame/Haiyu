using System.Text.Json.Serialization;
using MemoryPack;

namespace ABI.Models;

[MemoryPackable]
public partial record MonitorRecord(
    IReadOnlyList<CPUData> Cpus,
    IReadOnlyList<GPUData>? Gpus = null,
    MemoryData? Memory = null,
    VirtualMemoryData? VirtualMemory = null,
    IReadOnlyList<NetworkData>? Networks = null)
{
    /// <summary>兼容单路调用端，返回第一颗物理 CPU。</summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public CPUData? Cpu => Cpus.FirstOrDefault();
}

[MemoryPackable]
public partial record HardwareInfo(
    string Name,
    string Identifier,
    string HardwareType,
    IReadOnlyDictionary<string, string> Properties);

/// <summary>
/// CPU 数据
/// </summary>
[MemoryPackable]
public partial class CPUData
{
    public HardwareInfo Hardware { get; set; }
    /// <summary>
    /// CPU 电压
    /// </summary>
    public Dictionary<string,double> Voltages { get; set;  }

    /// <summary>
    /// CPU温度
    /// </summary>
    public double Temperature { get; set; }

    /// <summary>
    /// CPU占用负载
    /// </summary>
    public Dictionary<string,double> Load { get; set; }

    /// <summary>
    /// CPU时钟频率
    /// </summary>
    public Dictionary<string,double> Clock { get; set;  }
}

[MemoryPackable]
public partial class GPUData
{
    public HardwareInfo Hardware { get; set; }
    public Dictionary<string, double> Voltages { get; set; }
    public Dictionary<string, double> Temperatures { get; set; }
    public Dictionary<string, double> Load { get; set; }
    public Dictionary<string, double> Clock { get; set; }
    public Dictionary<string, double> Fans { get; set; }
    public Dictionary<string, double> Power { get; set; }
    public Dictionary<string, double> Memory { get; set; }
    public Dictionary<string, double> Throughput { get; set; }
    public Dictionary<string, double> Controls { get; set; }
    public Dictionary<string, double> Factors { get; set; }
    public Dictionary<string, double> Sensors { get; set; }
}

/// <summary>物理内存数据，容量单位由 LibreHardwareMonitor 提供（通常为 GB）。</summary>
[MemoryPackable]
public partial class MemoryData
{
    public HardwareInfo Hardware { get; set; }
    public double Used { get; set; }
    public double Available { get; set; }
    public double Total { get; set; }
    public double Load { get; set; }
}

/// <summary>虚拟内存数据，容量单位由 LibreHardwareMonitor 提供（通常为 GB）。</summary>
[MemoryPackable]
public partial class VirtualMemoryData
{
    public HardwareInfo Hardware { get; set; }
    public double Used { get; set; }
    public double Available { get; set; }
    public double Total { get; set; }
    public double Load { get; set; }
}

[MemoryPackable]
public partial class NetworkData
{
    public HardwareInfo Hardware { get; set; }
    public double UploadSpeed { get; set; }
    public double DownloadSpeed { get; set; }
    public double TotalUploaded { get; set; }
    public double TotalDownloaded { get; set; }
    public double Utilization { get; set; }
    public Dictionary<string, double> Sensors { get; set; }
}

[MemoryPackable]
public partial class FPSData
{
    [JsonPropertyName("forgroundProgramName")]
    public string ForgroundProgramName { get; set; }

    [JsonPropertyName("forgroudProgramFps")]
    public int FOrgroundProgramFps { get; set; }
}
