using System.Text.Json.Serialization;

namespace Waves.Api.Models.Maps;

public sealed class MapApiResponse<T>
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string Msg { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; set; }
}


public sealed class KuroRoleBindingInfoData
{
    [JsonPropertyName("userId")]
    public long UserId { get; set; }

    [JsonPropertyName("serverId")]
    public string ServerId { get; set; } = string.Empty;

    [JsonPropertyName("roleId")]
    public string RoleId { get; set; } = string.Empty;

    [JsonPropertyName("roleName")]
    public string RoleName { get; set; } = string.Empty;

    [JsonPropertyName("serverName")]
    public string ServerName { get; set; } = string.Empty;
}