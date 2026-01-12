using System.Runtime.CompilerServices;
using System.Text.Json;
using Waves.Core.Common;
using Waves.Core.Models;

namespace Waves.Core;

public class AppSettings:SettingBase
{
    public static string BassFolder =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Waves";

    public static string RecordFolder => BassFolder + "\\RecordCache";

    public static string WrallpaperFolder => BassFolder + "\\WallpaperImages";

    public static string ScreenCaptures => BassFolder + "\\ScreenCaptures";

    public static string ColorGameFolder => BassFolder + "\\ColorGameFolder";

    public static string LocalUserFolder=>BassFolder+"\\LocalUser";


    public string ToolsPosionFilePath => BassFolder + "\\ToolsPostion.json";

    private static readonly string SettingsFilePath = Path.Combine(BassFolder, "System.json");
    public static readonly string LogPath = BassFolder + "\\appLogs\\appLog.log";

    public static readonly string CloudFolderPath = BassFolder + "\\Cloud";

    public const string RpcVersion = "1.0";


    public AppSettings():base(SettingsFilePath)
    {
        LoadSettings();
    }

    
    public string? WallpaperType
    {
        get => Read();
        set => Write(value);
    }

    public string? AreaCounterPostion
    {
        get => Read();
        set => Write(value);
    }

    public string? AutoSignCommunity
    {
        get => Read();
        set => Write(value);
    }
    
    public string? LastSelectUser
    {
        get => Read();
        set=>Write(value);
    }

    public string? WallpaperPath
    {
        get => Read();
        set => Write(value);
    }
    public string? CloseWindow
    {
        get => Read();
        set => Write(value);
    }

    public string? SelectCursor
    {
        get => Read();
        set => Write(value);
    }

    public string? CaptureModifierKey
    {
        get => Read();
        set => Write(value);
    }

    public string? CaptureKey
    {
        get => Read();
        set => Write(value);
    }

    public string? IsCapture
    {
        get => Read();
        set => Write(value);
    }

    public string? Language
    {
        get => Read();
        set => Write(value);
    }

    public string? AutoOOBE
    {
        get => Read();
        set => Write(value);
    }
    public string ElementTheme
    {
        get => Read();
        set => Write(value);
    }

    public string? RpcToken
    {
        get => Read();
        set => Write(value);
    }
    public string? WavesAutoOpenContext
    {
        get => Read();
        set => Write(value);
    }
    public string? PunishAutoOpenContext
    {
        get => Read();
        set => Write(value);
    }

}
