using System.Text.Json.Serialization;
using Waves.Api.Models;
using Waves.Api.Models.Launcher;
using Waves.Core.Models;

namespace Haiyu.BackendService;

[JsonSerializable(typeof(BackendEvent))]
[JsonSerializable(typeof(List<BackendEvent>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(GameContextStatus))]
[JsonSerializable(typeof(GameContextConfig))]
[JsonSerializable(typeof(GameLauncherSource))]
[JsonSerializable(typeof(LIndex))]
[JsonSerializable(typeof(LauncherBackgroundData))]
[JsonSerializable(typeof(GameContextOutputArgs))]
[JsonSerializable(typeof(GameContextEventPayload))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string))]
public partial class BackendJsonContext : JsonSerializerContext
{
}

public record GameContextEventPayload(string ContextKey, GameContextOutputArgs Args);
