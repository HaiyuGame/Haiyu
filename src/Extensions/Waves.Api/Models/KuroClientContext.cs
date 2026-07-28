using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using Waves.Api.Models.KuroClient;

namespace Waves.Api.Models
{
    [JsonSerializable(typeof(KuroClientReturnCode<KuroClientSignInModel>))]
    [JsonSerializable(typeof(List<KuroClientSignInItem>))]
    [JsonSerializable(typeof(KuroClientReturnCode<KuroClientHomeFeedModel>))]
    [JsonSerializable(typeof(KuroClientReturnCode<bool>))]
    [JsonSerializable(typeof(KuroClientReturnCode<KuroClientPostPageDetail>))]
    [JsonSerializable(typeof(KuroClientReturnCode<KuroEncourageProcessModel>))]
    [JsonSerializable(typeof(KuroClientReturnCode<EncourageTotalGoldModel>))]
    public partial class KuroClientContext:JsonSerializerContext
    {
    }
}
