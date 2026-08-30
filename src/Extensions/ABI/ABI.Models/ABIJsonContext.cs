using System.Text.Json.Serialization;

namespace ABI.Models;

[JsonSerializable(typeof(CleanMemoryRequest))]
[JsonSerializable(typeof(RunResult))]
[JsonSerializable(typeof(CleanMemoryProgress))]
[JsonSerializable(typeof(RunResult))]
[JsonSerializable(typeof(CMonitorRequest))]
[JsonSerializable(typeof(CMonitorProgress))]
[JsonSerializable(typeof(CMonitorProgressData))]
[JsonSerializable(typeof(ABISystemConfigRequest))]
[JsonSerializable(typeof(ABISystemConfigProgress))]
public partial class ABIJsonContext : JsonSerializerContext
{
    
}



[JsonSerializable(typeof(PipeMessage))]
public partial class PipeJsonContext : JsonSerializerContext;
