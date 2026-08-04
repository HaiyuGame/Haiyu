using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Waves.Api.Models.CloudGame
{
    public class NetworkDetail
    {
        [JsonPropertyName("currentflow")]
        public int Currentflow { get; set; }

        [JsonPropertyName("bandWidth")]
        public int BandWidth { get; set; }

        [JsonPropertyName("netWorkDelay")]
        public int NetWorkDelay { get; set; }

        [JsonPropertyName("netWorkDelayUdp")]
        public int NetWorkDelayUdp { get; set; }

        [JsonPropertyName("latency")]
        public int Latency { get; set; }

        [JsonPropertyName("bitrate")]
        public int Bitrate { get; set; }

        [JsonPropertyName("serverFps")]
        public string ServerFps { get; set; }

        [JsonPropertyName("fps")]
        public int Fps { get; set; }

        [JsonPropertyName("decodeFps")]
        public int DecodeFps { get; set; }

        [JsonPropertyName("decodecTime")]
        public int DecodecTime { get; set; }

        [JsonPropertyName("renderFps")]
        public double RenderFps { get; set; }

        [JsonPropertyName("packetLoss")]
        public double PacketLoss { get; set; }

        [JsonPropertyName("packetLossRate")]
        public double PacketLossRate { get; set; }

        [JsonPropertyName("processingDelay")]
        public double ProcessingDelay { get; set; }

        [JsonPropertyName("jank")]
        public int Jank { get; set; }

        [JsonPropertyName("bigJank")]
        public int BigJank { get; set; }

        [JsonPropertyName("receiveAudioNum")]
        public int ReceiveAudioNum { get; set; }

        [JsonPropertyName("playAudioNum")]
        public int PlayAudioNum { get; set; }
    }

    public class WelinkMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("detail")]
        public NetworkDetail? Detail { get; set; }

        [JsonPropertyName("visible")]
        public bool Visible { get; set; }
    }


}
