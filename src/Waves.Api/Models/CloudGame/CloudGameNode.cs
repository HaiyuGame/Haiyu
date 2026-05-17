using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Waves.Api.Models.CloudGame;

public class CloudGameNode
{
    [JsonPropertyName("regionName")]
    public string RegionName { get; set; }

    [JsonPropertyName("regionDelay")]
    public int RegionDelay { get; set; }

    [JsonPropertyName("regionScore")]
    public int RegionScore { get; set; }

    [JsonPropertyName("regionState")]
    public int RegionState { get; set; }

    [JsonPropertyName("fastWaiting")]
    public int FastWaiting { get; set; }

    [JsonPropertyName("slowWaiting")]
    public int SlowWaiting { get; set; }

    [JsonPropertyName("nodeList")]
    public List<NodeList> NodeList { get; set; }
}

public class NodeList
{
    [JsonPropertyName("nodeId")]
    public string NodeId { get; set; }

    [JsonPropertyName("delay")]
    public int Delay { get; set; }
}

