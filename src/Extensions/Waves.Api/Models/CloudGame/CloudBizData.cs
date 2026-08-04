using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Waves.Api.Models.CloudGame;

/// <summary>
/// 云游戏进入请求数据
/// </summary>
public sealed partial class CloudBizData
{
    [JsonPropertyName("btype")]
    public string Btype { get; set; } = string.Empty;

    [JsonPropertyName("os")]
    public string WINDOWS { get; set; } = "WINDOWS";

    [JsonPropertyName("osVer")]
    public string OsVer { get; set; } = string.Empty;

    [JsonPropertyName("clientVer")]
    public string ClientVer { get; set; } = string.Empty;

    [JsonPropertyName("osCategory")]
    public string OsCategory { get; set; } = "H5";

    [JsonPropertyName("isOneLine")]
    public int IsOneLine { get; set; } = 1;

    [JsonPropertyName("extSDK")]
    public string ExtSdk { get; set; } = "{\"certHash\":true}";

    [JsonPropertyName("ping")]
    public IEnumerable<BizCloudNode> BizCloudNodes { get; set; }

    public CloudBizData(string osVer, string clientVer, IEnumerable<BizCloudNode> bizCloudNodes)
    {
        OsVer = osVer;
        this.ClientVer = clientVer;
        BizCloudNodes = bizCloudNodes;
    }
}

public class BizCloudNode
{
    [JsonPropertyName("nodeId")]
    public string NodeId { get; set; }

    [JsonPropertyName("result")]
    public string Result { get; set; }
}
