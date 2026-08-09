using System.Text.Json.Serialization;

namespace Waves.Api.Models.Communitys
{
    public partial class AccountModel
    {
        [JsonPropertyName("code")]
        public long Code { get; set; }

        [JsonPropertyName("data")]
        public AccountData Data { get; set; }

        [JsonPropertyName("msg")]
        public string Msg { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }

    public partial class SMSResultModel
    {
        [JsonPropertyName("code")]
        public long Code { get; set; }

        [JsonPropertyName("data")]
        public SMSResult Data { get; set; }

        [JsonPropertyName("msg")]
        public string Msg { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }

    public partial class AccountData
    {
        [JsonPropertyName("enableChildMode")]
        public bool EnableChildMode { get; set; }

        [JsonPropertyName("gender")]
        public long Gender { get; set; }

        [JsonPropertyName("signature")]
        public string Signature { get; set; }

        [JsonPropertyName("headUrl")]
        public Uri HeadUrl { get; set; }

        [JsonPropertyName("headCode")]
        public string HeadCode { get; set; }

        [JsonPropertyName("userName")]
        public string UserName { get; set; }

        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("isRegister")]
        public long IsRegister { get; set; }

        [JsonPropertyName("isOfficial")]
        public long IsOfficial { get; set; }

        [JsonPropertyName("status")]
        public long Status { get; set; }

        [JsonPropertyName("unRegistering")]
        public bool UnRegistering { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }
    }

    public class SMSResult
    {
        [JsonPropertyName("geeTest")]
        public bool GeeTest { get; set; }
    }
}
