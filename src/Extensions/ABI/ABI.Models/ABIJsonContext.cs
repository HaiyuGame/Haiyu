using System.Text.Json.Serialization;

namespace ABI.Models;

[JsonSerializable(typeof(CleanMemoryRequest))]
[JsonSerializable(typeof(RunResult))]
[JsonSerializable(typeof(CleanMemoryProgress))]
[JsonSerializable(typeof(RunResult))]
[JsonSerializable(typeof(CMonitorRequest))]
[JsonSerializable(typeof(CMonitorProgress))]
[JsonSerializable(typeof(FpsMonitorRequest))]
[JsonSerializable(typeof(FpsMonitorProgress))]
[JsonSerializable(typeof(MonitorRecord))]
[JsonSerializable(typeof(CPUData))]
[JsonSerializable(typeof(GPUData))]
[JsonSerializable(typeof(MemoryData))]
[JsonSerializable(typeof(VirtualMemoryData))]
[JsonSerializable(typeof(NetworkData))]
[JsonSerializable(typeof(HardwareInfo))]
[JsonSerializable(typeof(ABISystemConfigRequest))]
[JsonSerializable(typeof(ABISystemConfigProgress))]
public partial class ABIJsonContext : JsonSerializerContext
{
    
}



[JsonSerializable(typeof(PipeMessage))]
[JsonSerializable(typeof(OpenRequestMessage))]
public partial class PipeJsonContext : JsonSerializerContext;
