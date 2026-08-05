using System.Text.Json.Serialization;

namespace Waves.Settings;

[Settings<string>(Name = "WallpaperType", Nullable = true)]
[Settings<string>(Name = "AreaCounterPostion", Nullable = true)]
[Settings<bool>(Name = "AutoSignCommunity", Nullable = true, DefaultValue = "False")]
[Settings<bool>(Name = "AutoKuroTaskEnable", Nullable = true, DefaultValue = "False")]
[Settings<string>(Name = "LastSelectUser", Nullable = true)]
[Settings<string>(Name = "WallpaperPath", Nullable = true)]
[Settings<string>(Name = "CloseWindow", Nullable = true)]
[Settings<string>(Name = "SelectCursor", Nullable = true)]
[Settings<string>(Name = "CaptureModifierKey", Nullable = true)]
[Settings<string>(Name = "CaptureKey", Nullable = true)]
[Settings<string>(Name = "IsCapture", Nullable = true)]
[Settings<string>(Name = "Language", Nullable = true)]
[Settings<bool>(Name = "AutoOOBE", Nullable = true, DefaultValue = "True")]
[Settings<string>(Name = "ElementTheme")]
[Settings<string>(Name = "RpcToken", Nullable = true)]
[Settings<string>(Name = "WavesAutoOpenContext", Nullable = true)]
[Settings<string>(Name = "PunishAutoOpenContext", Nullable = true)]
[Settings<string>(Name = "UpdateType", Nullable = true, DefaultValue = "Github")]
[Settings<string>(Name = "SkipAppVersion", Nullable = true)]
[Settings<bool>(Name = "StartGameAllowCloseMain", Nullable = true, DefaultValue = "False")]
[Settings<string>(Name = "MirrorKey", Nullable = true)]
[Settings<string>(Name = "LauncheBth", Nullable = true, DefaultValue = "Home")]
[Settings<List<string>>(Name ="skipVerifyFiles",
    JsonTypeInfoContextType = typeof(AppSettingJsonContext),
    JsonTypeInfoPropertyName = nameof(AppSettingJsonContext.Default.ListString))]
[Settings<bool>(Name ="verifySkilDelete",Nullable = false,DefaultValue ="true")]
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
        await WriteAsync(Math.Clamp(value, 1, 4).ToString(), "MaxIoConcurrent", ct)
            .ConfigureAwait(false);
    }
}


[JsonSerializable(typeof(List<string>))]
public partial class AppSettingJsonContext:JsonSerializerContext
{

}
