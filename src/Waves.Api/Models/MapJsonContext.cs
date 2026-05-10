using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using Waves.Api.Models.Maps;

namespace Waves.Api.Models
{
    [JsonSerializable(typeof(MapApiResponse<bool>))]
    [JsonSerializable(typeof(MapApiResponse<KuroRoleBindingInfoData>))]
    public partial class MapJsonContext : JsonSerializerContext
    {
    }
}
