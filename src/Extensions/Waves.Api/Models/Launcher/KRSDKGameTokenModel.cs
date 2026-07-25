using System.Text.Json.Serialization;

namespace Waves.Api.Models.Launcher;

public class KRSDKGameTokenCache
{
    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("cuid")]
    public string Cuid { get; set; }

    [JsonPropertyName("id")]
    public double Id { get; set; }

    [JsonPropertyName("localCachedThirdLoginParams")]
    public string LocalCachedThirdLoginParams { get; set; }

    [JsonPropertyName("loginType")]
    public int LoginType { get; set; }

    [JsonPropertyName("phone")]
    public string Phone { get; set; }

    [JsonPropertyName("phoneCheck")]
    public int PhoneCheck { get; set; }

    [JsonPropertyName("scanChannelId")]
    public int ScanChannelId { get; set; }

    [JsonPropertyName("thirdNickName")]
    public string ThirdNickName { get; set; }

    [JsonPropertyName("token")]
    public string Token { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; }
}

public class KRSDKGameTokenModel
{
    [JsonPropertyName("account_list")]
    public List<KRSDKGameTokenCache> AccountList { get; set; }

    [JsonPropertyName("last_login_cuid")]
    public string LastLoginCuid { get; set; }
}


