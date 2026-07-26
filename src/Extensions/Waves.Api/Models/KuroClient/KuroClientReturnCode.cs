using System.Text.Json.Serialization;

namespace Waves.Api.Models.KuroClient;

public class KuroClientReturnCode<T>
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("data")]
    public T Data { get; set; }

    [JsonPropertyName("msg")]
    public string Msg { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("traceId")]
    public string TraceId { get; set; }
}
