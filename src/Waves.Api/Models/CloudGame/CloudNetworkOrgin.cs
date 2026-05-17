using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
namespace Waves.Api.Models.CloudGame
{
    // Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);
    public class CloudNetworkOrginItem
    {
        [JsonPropertyName("nodeName")]
        public string NodeName { get; set; }

        [JsonPropertyName("lineId")]
        public string LineId { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("singleLine")]
        public string SingleLine { get; set; }

        [JsonPropertyName("lineH5Port")]
        public string LineH5Port { get; set; }

        [JsonPropertyName("provide")]
        public string Provide { get; set; }

        [JsonPropertyName("nodeAlias")]
        public string NodeAlias { get; set; }

        [JsonPropertyName("lineH5Addr")]
        public string LineH5Addr { get; set; }

        [JsonPropertyName("nodeId")]
        public string NodeId { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }
    }

    public class CloudNetworkOrgin
    {
        [JsonPropertyName("broadbandUrl")]
        public string BroadbandUrl { get; set; }

        [JsonPropertyName("bandwidthTime")]
        public string BandwidthTime { get; set; }

        [JsonPropertyName("nodeRefreshTime")]
        public string NodeRefreshTime { get; set; }

        [JsonPropertyName("broadbandUrlBak")]
        public string BroadbandUrlBak { get; set; }

        [JsonPropertyName("pingNum")]
        public string PingNum { get; set; }

        [JsonPropertyName("ispTimeOut")]
        public string IspTimeOut { get; set; }

        [JsonPropertyName("TenantConfig")]
        public string TenantConfig { get; set; }

        [JsonPropertyName("getIspNum")]
        public string GetIspNum { get; set; }

        [JsonPropertyName("lines")]
        public List<CloudNetworkOrginItem> Lines { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }

        [JsonPropertyName("ext")]
        public string Ext { get; set; }

        [JsonPropertyName("DestroyOperateCodecConfig")]
        public string DestroyOperateCodecConfig { get; set; }

        [JsonPropertyName("pingMaxDownloadData")]
        public string PingMaxDownloadData { get; set; }

        [JsonPropertyName("AudioPlayMode")]
        public string AudioPlayMode { get; set; }

        [JsonPropertyName("speed_use_cache")]
        public int SpeedUseCache { get; set; }

        [JsonPropertyName("threads")]
        public string Threads { get; set; }

        [JsonPropertyName("openExtraSensor")]
        public string OpenExtraSensor { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("timeOut")]
        public string TimeOut { get; set; }

        [JsonPropertyName("velocityDiffer")]
        public string VelocityDiffer { get; set; }

        [JsonPropertyName("showNoOperationTime")]
        public string ShowNoOperationTime { get; set; }

        [JsonPropertyName("isNewSpeed")]
        public string IsNewSpeed { get; set; }

        [JsonPropertyName("broadbandInterval")]
        public string BroadbandInterval { get; set; }
    }

    public class CloudNetworkRequest
    {
        [JsonPropertyName("nodeId")]
        public string NodeId { get; set; }

        [JsonPropertyName("delay")]
        public int Delay { get; set; }

    }
}
