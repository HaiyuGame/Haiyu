namespace Waves.Settings;

/// <summary>
/// Haiyu RPC 服务相关配置（网络范围、端口、授权等）。
/// </summary>
[SettingsAttribute<int>(Name = "RpcLocalPort", Nullable = false, DefaultValue = "10010")]
[SettingsAttribute<string>(Name = "NetworkScope", Nullable = false, DefaultValue = "Loopback")]
[SettingsAttribute<bool>(Name = "RequireAuth", Nullable = false, DefaultValue = "True")]
[SettingsAttribute<string>(Name = "AuthToken", Nullable = true)]
public partial class RpcSettings : SettingBase
{
    private static readonly string DefaultConfigPath = Path.Combine(
        AppSettings.BassFolder,
        "rpc.json"
    );

    public RpcSettings()
        : base(DefaultConfigPath)
    {
        _ = LoadSettingsAsync();
    }

    public RpcSettings(string configPath)
        : base(configPath)
    {
        _ = LoadSettingsAsync();
    }
}
