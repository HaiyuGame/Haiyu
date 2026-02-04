using MemoryPack;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Waves.Core.Models;

public class GameLocalSettingName
{
    /// <summary>
    /// 游戏启动文件夹
    /// </summary>
    public const string GameLauncherBassFolder = nameof(GameLauncherBassFolder);

    /// <summary>
    /// 游戏启动可执行文件
    /// </summary>
    public const string GameLauncherBassProgram = nameof(GameLauncherBassProgram);

    /// <summary>
    /// 本地游戏版本
    /// </summary>
    public const string LocalGameVersion = nameof(LocalGameVersion);

    public const string LocalGameUpdateing = nameof(LocalGameUpdateing);

    /// <summary>
    /// 下载速度
    /// </summary>
    public const string LimitSpeed = nameof(LimitSpeed);

    /// <summary>
    /// 是否使用DX11启动
    /// </summary>
    public const string IsDx11 = nameof(IsDx11);

    public const string GameRunTotalTime = nameof(GameRunTotalTime);

    /// <summary>
    /// 预下载路径
    /// </summary>
    public const string ProdDownloadFolderPath = nameof(ProdDownloadFolderPath);
    public const string ProdDownloadFolderDone = nameof(ProdDownloadFolderDone);

    /// <summary>
    /// 预下载路径
    /// </summary>
    public const string ProdDownloadPath = nameof(ProdDownloadPath);

    /// <summary>
    /// 预下载版本
    /// </summary>
    public const string ProdDownloadVersion = nameof(ProdDownloadVersion);

    public const string GameTime = nameof(GameTime);
}


public class GameLocalConfig
{
    private Dictionary<string, string> _settings = new Dictionary<string, string>();
    private readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

    private readonly LocalSettingsJsonContext _jsonContext = new LocalSettingsJsonContext(
        new JsonSerializerOptions
        {
            WriteIndented = true
        }
    );

    public string SettingPath { get; set; }

    public GameLocalConfig(string settingPath)
    {
        SettingPath = settingPath;
    }

    /// <summary>
    /// 从JSON文件加载配置到内存
    /// </summary>
    private async Task LoadConfigAsync(CancellationToken token)
    {
        if (!File.Exists(SettingPath))
        {
            _settings = new Dictionary<string, string>();
            return;
        }

        try
        {
            var jsonString = await File.ReadAllTextAsync(SettingPath,token);

            var loadedSettings = JsonSerializer.Deserialize(jsonString, typeof(Dictionary<string, string>), _jsonContext) as Dictionary<string, string>;
            _settings = loadedSettings ?? new Dictionary<string, string>();
        }
        catch
        {
            _settings = new Dictionary<string, string>();
        }
    }

    private async Task SaveConfigToFileAsync(CancellationToken token = default)
    {
        await _fileLock.WaitAsync();
        try
        {
            var jsonString = JsonSerializer.Serialize(_settings, typeof(Dictionary<string, string>), _jsonContext);
            await File.WriteAllTextAsync(SettingPath, jsonString,token);

            await LoadConfigAsync(token);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public async Task<bool> SaveConfigAsync(string key, string value,CancellationToken token = default)
    {
        try
        {
            _settings[key] = value;
            await SaveConfigToFileAsync(token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取配置
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public async Task<string?> GetConfigAsync(string key,CancellationToken token = default)
    {
        await LoadConfigAsync(token);
        if (_settings.TryGetValue(key, out string? value))
        {
            return value;
        }

        return null;
    }
}

public class LocalSettings
{
    [JsonPropertyName("key")]
    public string Key { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; }
}



[JsonSerializable(typeof(LocalSettings))]
[JsonSerializable(typeof(List<LocalSettings>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class LocalSettingsJsonContext : JsonSerializerContext { };
