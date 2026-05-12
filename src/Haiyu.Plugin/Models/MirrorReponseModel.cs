using System.Text.Json.Serialization;

namespace Haiyu.Plugin.Models;

public class MirrorData
{
    [JsonPropertyName("version_name")]
    public string VersionName { get; set; }

    [JsonPropertyName("version_number")]
    public int VersionNumber { get; set; }


    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; }

    [JsonPropertyName("channel")]
    public string Channel { get; set; }

    [JsonPropertyName("os")]
    public string Os { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("arch")]
    public string Arch { get; set; }

    [JsonPropertyName("update_type")]
    public string UpdateType { get; set; }

    [JsonPropertyName("release_note")]
    public string ReleaseNote { get; set; }

    [JsonPropertyName("filesize")]
    public int Filesize { get; set; }

    [JsonPropertyName("cdk_expired_time")]
    public int CdkExpiredTime { get; set; }
}

public class MirrorReponseModel
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string Msg { get; set; }

    [JsonPropertyName("data")]
    public MirrorData Data { get; set; }
}
