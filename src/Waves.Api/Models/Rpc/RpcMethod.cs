using System.Reflection;
using System.Runtime.Serialization;

namespace Waves.Api.Models.Rpc;

public enum RpcMethod
{
    [EnumMember(Value = "backend_ping")]
    BackendPing,
    [EnumMember(Value = "backend_poll_events")]
    BackendPollEvents,
    [EnumMember(Value = "backend_launch_process")]
    BackendLaunchProcess,
    [EnumMember(Value = "gamecontext_list")]
    GameContextList,
    [EnumMember(Value = "gamecontext_get_status")]
    GameContextGetStatus,
    [EnumMember(Value = "gamecontext_get_default_launcher")]
    GameContextGetDefaultLauncher,
    [EnumMember(Value = "gamecontext_get_background")]
    GameContextGetBackground,
    [EnumMember(Value = "gamecontext_get_launcher_source")]
    GameContextGetLauncherSource,
    [EnumMember(Value = "gamecontext_read_config")]
    GameContextReadConfig,
    [EnumMember(Value = "gamecontext_start_download")]
    GameContextStartDownload,
    [EnumMember(Value = "gamecontext_pause_download")]
    GameContextPauseDownload,
    [EnumMember(Value = "gamecontext_resume_download")]
    GameContextResumeDownload,
    [EnumMember(Value = "gamecontext_stop_download")]
    GameContextStopDownload,
    [EnumMember(Value = "gamecontext_set_speed_limit")]
    GameContextSetSpeedLimit,
    [EnumMember(Value = "gamecontext_start_game")]
    GameContextStartGame,
    [EnumMember(Value = "gamecontext_stop_game")]
    GameContextStopGame
}

public static class RpcMethodExtensions
{
    public static string ToRpcName(this RpcMethod method)
    {
        var member = typeof(RpcMethod).GetMember(method.ToString()).FirstOrDefault();
        var attribute = member?.GetCustomAttribute<EnumMemberAttribute>();
        return attribute?.Value ?? method.ToString();
    }
}
