using System.Text.Json.Serialization;

namespace Waves.Api.Models.CloudGame;


public class BridgeConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("tenantKey")]
    public string TenantKey { get; set; } = string.Empty;

    [JsonPropertyName("IspUrl")]
    public string IspUrl { get; set; } = string.Empty;

    [JsonPropertyName("videoPoster")]
    public string VideoPoster { get; set; } = string.Empty;

    [JsonPropertyName("gameId")]
    public string GameId { get; set; } = string.Empty;

    [JsonPropertyName("nodeId")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("enableClipBoard")]
    public bool EnableClipBoard { get; set; }

    [JsonPropertyName("mouseShortcut")]
    public string? MouseShortcut { get; set; }

    [JsonPropertyName("lockPoint")]
    public bool LockPoint { get; set; }

    [JsonPropertyName("envType")]
    public string EnvType { get; set; } = string.Empty;

    [JsonPropertyName("fillVideo")]
    public bool FillVideo { get; set; }

    [JsonPropertyName("enableInitSpeed")]
    public bool EnableInitSpeed { get; set; }

    [JsonPropertyName("useGamePlayLayer")]
    public bool UseGamePlayLayer { get; set; }

    [JsonPropertyName("enableReplenishEsc")]
    public bool EnableReplenishEsc { get; set; }

    [JsonPropertyName("enableReportLog")]
    public bool EnableReportLog { get; set; }

    [JsonPropertyName("enableReconnect")]
    public bool EnableReconnect { get; set; }

    [JsonPropertyName("bitRate")]
    public int BitRate { get; set; }

    [JsonPropertyName("bitRateMin")]
    public int BitRateMin { get; set; }

    [JsonPropertyName("bitRateMax")]
    public int BitRateMax { get; set; }

    [JsonPropertyName("fps")]
    public int Fps { get; set; }

    [JsonPropertyName("targetWidth")]
    public int TargetWidth { get; set; }

    [JsonPropertyName("targetHeight")]
    public int TargetHeight { get; set; }

    [JsonPropertyName("codecType")]
    public int CodecType { get; set; } = 0;

    [JsonPropertyName("streamStrategy")]
    public string StreamStrategy { get; set; } = string.Empty;

    [JsonPropertyName("enableImageEnhancement")]
    public bool EnableImageEnhancement { get; set; }

    [JsonPropertyName("dpi")]
    public int Dpi { get; set; }
}
