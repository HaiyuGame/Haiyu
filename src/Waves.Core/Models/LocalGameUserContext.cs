using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Waves.Core.Models
{
    [JsonSerializable(typeof(WavesQueryLocalPlayerInfoRequest))]
    [JsonSerializable(typeof(QueryPlayerInfo))]
    [JsonSerializable(typeof(WavesQueryPlayerItem))]
    [JsonSerializable(typeof(QueryLocalRoleInfoRequest))]
    [JsonSerializable(typeof(QueryRoleInfo))]
    [JsonSerializable(typeof(WavesLocalGameRoleItem))]
    //战双
    [JsonSerializable(typeof(PunishQueryPlayerItem))]
    [JsonSerializable(typeof(PunishLocalGameRoleItem))]
    public partial class LocalGameUserContext:JsonSerializerContext
    {
    }
}
