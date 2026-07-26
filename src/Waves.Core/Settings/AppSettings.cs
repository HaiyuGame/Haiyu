using Haiyu.Analyzers;

namespace Waves.Core.Settings;

[SettingsAttribute<string>(Name = "WallpaperType", Nullable = true)]
[SettingsAttribute<string>(Name = "AreaCounterPostion", Nullable = true)]
[SettingsAttribute<bool>(Name = "AutoSignCommunity", Nullable = true, DefaultValue = "False")]
[SettingsAttribute<bool>(Name = "AutoKuroTaskEnable", Nullable = true, DefaultValue = "False")]
[SettingsAttribute<string>(Name = "LastSelectUser", Nullable = true)]
[SettingsAttribute<string>(Name = "WallpaperPath", Nullable = true)]
[SettingsAttribute<string>(Name = "CloseWindow", Nullable = true)]
[SettingsAttribute<string>(Name = "SelectCursor", Nullable = true)]
[SettingsAttribute<string>(Name = "CaptureModifierKey", Nullable = true)]
[SettingsAttribute<string>(Name = "CaptureKey", Nullable = true)]
[SettingsAttribute<string>(Name = "IsCapture", Nullable = true)]
[SettingsAttribute<string>(Name = "Language", Nullable = true)]
[SettingsAttribute<string>(Name = "AutoOOBE", Nullable = true)]
[SettingsAttribute<string>(Name = "ElementTheme")]
[SettingsAttribute<string>(Name = "RpcToken", Nullable = true)]
[SettingsAttribute<string>(Name = "WavesAutoOpenContext", Nullable = true)]
[SettingsAttribute<string>(Name = "PunishAutoOpenContext", Nullable = true)]
[SettingsAttribute<string>(Name = "UpdateType", Nullable = true, DefaultValue = "Github")]
[SettingsAttribute<string>(Name = "SkipAppVersion", Nullable = true)]
[SettingsAttribute<bool>(Name = "StartGameAllowCloseMain", Nullable = true, DefaultValue = "False")]
[SettingsAttribute<string>(Name = "MirrorKey", Nullable = true)]
[SettingsAttribute<string>(Name = "LauncheBth", Nullable = true, DefaultValue = "Home")]
public partial class AppSettings : SettingBase
{
    public static string BassFolder =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Waves";

    public static string RecordFolder => BassFolder + "\\RecordCache";

    public static string WavesRecordFolder => BassFolder + "\\WavesRecordCache";

    public static string WrallpaperFolder => BassFolder + "\\WallpaperImages";

    public static string ScreenCaptures => BassFolder + "\\ScreenCaptures";

    public static string WebCacheFolder => BassFolder + "\\WebCache";

    public static string ColorGameFolder => BassFolder + "\\ColorGameFolder";

    public static string LocalUserFolder => BassFolder + "\\LocalUser";

    public string ToolsPosionFilePath => BassFolder + "\\ToolsPostion.json";

    private static readonly string SettingsFilePath = Path.Combine(BassFolder, "System.json");
    public static readonly string LogPath = BassFolder + "\\appLogs\\appLog.log";

    public static readonly string CloudFolderPath = BassFolder + "\\Cloud";

    public const string RpcVersion = "1.0";

    public AppSettings()
        : base(SettingsFilePath)
    {
        _ = LoadSettingsAsync();
    }

    public async Task<int> GetMaxIoConcurrentAsync(CancellationToken ct = default)
    {
        var val = await ReadAsync("MaxIoConcurrent", ct).ConfigureAwait(false);
        return int.TryParse(val, out var r) ? r : 1;
    }

    public async Task SetMaxIoConcurrentAsync(int value, CancellationToken ct = default)
    {
        await WriteAsync(Math.Clamp(value, 1, 4).ToString(), "MaxIoConcurrent", ct).ConfigureAwait(false);
    }
}
