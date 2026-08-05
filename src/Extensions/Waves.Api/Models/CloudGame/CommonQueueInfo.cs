using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Waves.Api.Models.CloudGame;


public class CommonQueueInfo
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string Msg { get; set; }

    [JsonPropertyName("providerType")]
    public int ProviderType { get; set; }

    [JsonPropertyName("regionName")]
    public string RegionName { get; set; }

    [JsonPropertyName("dispatchResult")]
    public DispatchResult DispatchResult { get; set; }

    [JsonPropertyName("seatNo")]
    public int SeatNo { get; set; }

    [JsonPropertyName("waitingTime")]
    public int WaitingTime { get; set; }
}

public class DispatchResult
{
    [JsonPropertyName("dispatchMsg")]
    public string DispatchMsg { get; set; }

    [JsonPropertyName("roundId")]
    public string RoundId { get; set; }

    [JsonPropertyName("reservedId")]
    public string ReservedId { get; set; }

    [JsonPropertyName("userKey")]
    public string UserKey { get; set; }

    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; }

    [JsonPropertyName("allocRespJson")]
    public string AllocRespJson { get; set; }

    [JsonPropertyName("tk")]
    public string Tk { get; set; }
}
