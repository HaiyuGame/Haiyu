using System.Text.Json.Serialization;

namespace Waves.Api.Models.Rpc;

public class RpcReponse
{
    [JsonPropertyName("requestId")]
    public long RequestId { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set;  }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}
