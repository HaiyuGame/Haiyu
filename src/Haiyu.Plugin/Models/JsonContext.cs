using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Haiyu.Plugin.Models;

[JsonSerializable(typeof(MirrorReponseModel))]
[JsonSerializable(typeof(GithubResponseModel))]
[JsonSerializable(typeof(List<GithubResponseModel>))]
internal partial class JsonContext:JsonSerializerContext
{
}
