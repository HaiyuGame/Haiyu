using System.Text.Json.Serialization;

namespace KuroGameDownloadProgram;

[JsonSerializable(typeof(List<string>))]
public partial class DownloadJsonContext : JsonSerializerContext
{
}
