using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Waves.Core.Models
{
    [JsonSerializable(typeof(QueryLocalPlayerInfoRequest))]
    [JsonSerializable(typeof(QueryPlayerInfo))]
    [JsonSerializable(typeof(QueryPlayerItem))]
    [JsonSerializable(typeof(QueryLocalRoleInfoRequest))]
    [JsonSerializable(typeof(QueryRoleInfo))]
    [JsonSerializable(typeof(LocalGameRoleItem))]
    public partial class LocalGameUserContext:JsonSerializerContext
    {
    }
}
