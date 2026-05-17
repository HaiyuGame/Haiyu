using System.Text.Json.Serialization;

namespace Waves.Api.Models.CloudGame;

public class CloudApiResponse<T>
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string Msg { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }


    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; set; }
}
