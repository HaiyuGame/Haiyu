using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Haiyu.Models
{
    public sealed class WebSessionContext(string token, string did, string userId, string serverId, string roleId)
    {
        //https://web-static.kurobbs.com/mcbox/index.html#/mc-role-box?accessType=1&roleId=104370585&serverId=76402e5b20be2c39f095a152090afddc
        public string Token => token;

        public string Did => did;

        public string UserId => userId;

        public string ServerId => serverId;

        public string RoleId => roleId;

        public string GetDataCenterUrl()
        {
            return $"https://web-static.kurobbs.com/mcbox/index.html#/mc-role-box?accessType=1&roleId={RoleId}&serverId={ServerId}";
        }
    }

    public sealed class KuroSession()
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }


        [JsonPropertyName("did")]
        public string Did { get; set; }

        [JsonPropertyName("userId")]
        public string UserId { get; set; }
    }

    [JsonSerializable(typeof(KuroSession))]
    public partial class KuroSessionContext : JsonSerializerContext
    {
    }
}
