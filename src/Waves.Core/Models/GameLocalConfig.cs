using Waves.Settings;

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
    /// 本地游戏最后选择的用户名称
    /// </summary>
    public const string LasterSelectLocalUser = nameof(LasterSelectLocalUser);

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

    public const string DisableDlss = nameof(DisableDlss);

    public const string StartGameArguments = nameof(StartGameArguments);

    public const string StartGameExeName = nameof(StartGameExeName);

    public const string GameRunTotalTime = nameof(GameRunTotalTime);

    /// <summary>
    /// 预下载完成
    /// </summary>
    public const string ProdDownloadFolderDone = nameof(ProdDownloadFolderDone);

    /// <summary>
    /// 预下载路径
    /// </summary>
    public const string ProdDownloadPath = nameof(ProdDownloadPath);

    /// <summary>
    /// 预下载版本
    /// </summary>
    public const string ProdDownloadVersion = nameof(ProdDownloadVersion);

    /// <summary>
    /// 是否已经安装了ProdIsAdvance版本
    /// </summary>
    public const string ProdIsAdvance = nameof(ProdIsAdvance);

    public const string GameTime = nameof(GameTime);
}


public class GameLocalConfig
{
    private Dictionary<string, string> _settings = new Dictionary<string, string>();
    private readonly object _settingsGate = new();
    private readonly SemaphoreSlim _ioLock = new SemaphoreSlim(1, 1);
    private bool _loaded;
    private long _cachedLastWriteTicks = -1;
    private long _cachedLength = -1;

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
    private (long LastWriteTicks, long Length) GetFileStamp()
    {
        var fileInfo = new FileInfo(SettingPath);
        if (!fileInfo.Exists)
        {
            return (-1, -1);
        }

        return (fileInfo.LastWriteTimeUtc.Ticks, fileInfo.Length);
    }

    private bool IsCacheFresh((long LastWriteTicks, long Length) fileStamp)
    {
        return _loaded
            && _cachedLastWriteTicks == fileStamp.LastWriteTicks
            && _cachedLength == fileStamp.Length;
    }

    private async Task EnsureLoadedAsync(CancellationToken token)
    {
        var fileStamp = GetFileStamp();
        lock (_settingsGate)
        {
            if (IsCacheFresh(fileStamp))
            {
                return;
            }
        }

        await _ioLock.WaitAsync(token);
        try
        {
            fileStamp = GetFileStamp();
            lock (_settingsGate)
            {
                if (IsCacheFresh(fileStamp))
                {
                    return;
                }
            }

            var loadedSettings = await LoadConfigAsync(token);
            lock (_settingsGate)
            {
                _settings = loadedSettings;
                _cachedLastWriteTicks = fileStamp.LastWriteTicks;
                _cachedLength = fileStamp.Length;
                _loaded = true;
            }
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task<Dictionary<string, string>> LoadConfigAsync(CancellationToken token)
    {
        if (!File.Exists(SettingPath))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var jsonString = await File.ReadAllTextAsync(SettingPath, token);

            var loadedSettings =
                JsonSerializer.Deserialize(
                    jsonString,
                    typeof(Dictionary<string, string>),
                    _jsonContext
                ) as Dictionary<string, string>;
            return loadedSettings ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private async Task SaveConfigToFileAsync(
        IReadOnlyDictionary<string, string> settings,
        CancellationToken token = default
    )
    {
        var directory = Path.GetDirectoryName(SettingPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var jsonString = JsonSerializer.Serialize(
            settings,
            typeof(Dictionary<string, string>),
            _jsonContext
        );

        var tempPath = $"{SettingPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, jsonString, token);
            File.Move(tempPath, SettingPath, true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best effort cleanup. The config has already been written or the original exception should surface.
            }
        }
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public async Task<bool> SaveConfigAsync(string key, string value, CancellationToken token = default)
    {
        return await SaveConfigsAsync(new Dictionary<string, string> { [key] = value }, token);
    }

    public async Task<bool> SaveConfigsAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken token = default
    )
    {
        await _ioLock.WaitAsync(token);
        try
        {
            var fileStamp = GetFileStamp();
            Dictionary<string, string>? settings = null;
            lock (_settingsGate)
            {
                if (IsCacheFresh(fileStamp))
                {
                    settings = new Dictionary<string, string>(_settings);
                }
            }

            settings ??= await LoadConfigAsync(token);

            foreach (var item in values)
            {
                settings[item.Key] = item.Value;
            }

            await SaveConfigToFileAsync(settings, token);

            var savedStamp = GetFileStamp();
            lock (_settingsGate)
            {
                _settings = settings;
                _cachedLastWriteTicks = savedStamp.LastWriteTicks;
                _cachedLength = savedStamp.Length;
                _loaded = true;
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    /// <summary>
    /// 获取配置
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public async Task<string?> GetConfigAsync(string key, CancellationToken token = default)
    {
        await EnsureLoadedAsync(token);
        lock (_settingsGate)
        {
            if (_settings.TryGetValue(key, out string? value))
            {
                return value;
            }

            return null;
        }
    }
}

