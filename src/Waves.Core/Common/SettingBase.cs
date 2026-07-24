using System.Text.Json.Serialization.Metadata;

namespace Waves.Core.Common;

public abstract class SettingBase
{
    private readonly string _configPath;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private Dictionary<string, string> _settingsCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;
    private long _cachedLastWriteTicks = -1;
    private long _cachedLength = -1;

    protected SettingBase(string configPath)
    {
        _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
    }

    protected virtual JsonTypeInfo? GetJsonTypeInfo(string key) => null;

    public async Task<string?> ReadAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("配置键名不能为空", nameof(key));

        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        await _ioLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return _settingsCache.TryGetValue(key, out var value) ? value : null;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task WriteAsync(string? value, string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("配置键名不能为空", nameof(key));

        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        await _ioLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (value is null)
                _settingsCache.Remove(key);
            else
                _settingsCache[key] = value;

            await SaveSettingsAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task LoadSettingsAsync(CancellationToken ct = default)
    {
        await _ioLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await DoLoadSettingsAsync(ct).ConfigureAwait(false);
            _loaded = true;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private (long LastWriteTicks, long Length) GetFileStamp()
    {
        var fileInfo = new FileInfo(_configPath);
        if (!fileInfo.Exists)
            return (-1, -1);

        return (fileInfo.LastWriteTimeUtc.Ticks, fileInfo.Length);
    }

    private bool IsCacheFresh((long LastWriteTicks, long Length) stamp)
    {
        return _loaded
            && _cachedLastWriteTicks == stamp.LastWriteTicks
            && _cachedLength == stamp.Length;
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        var stamp = GetFileStamp();
        if (IsCacheFresh(stamp))
            return;

        await _ioLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            stamp = GetFileStamp();
            if (IsCacheFresh(stamp))
                return;

            await DoLoadSettingsAsync(ct).ConfigureAwait(false);
            _cachedLastWriteTicks = stamp.LastWriteTicks;
            _cachedLength = stamp.Length;
            _loaded = true;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task DoLoadSettingsAsync(CancellationToken ct)
    {
        if (!File.Exists(_configPath))
        {
            _settingsCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_configPath, ct).ConfigureAwait(false);
            var settingsList = JsonSerializer.Deserialize(
                json,
                LocalSettingsJsonContext.Default.ListLocalSettings
            );

            if (settingsList is { Count: > 0 })
            {
                _settingsCache = settingsList.ToDictionary(
                    x => x.Key,
                    x => x.Value,
                    StringComparer.OrdinalIgnoreCase
                );
            }
            else
            {
                _settingsCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            _settingsCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveSettingsAsync(CancellationToken ct)
    {
        var settingsList = _settingsCache.Select(kv => new LocalSettings
        {
            Key = kv.Key,
            Value = kv.Value
        }).ToList();

        var json = JsonSerializer.Serialize(
            settingsList,
            LocalSettingsJsonContext.Default.ListLocalSettings
        );

        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var tempPath = _configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);
            File.Move(tempPath, _configPath, true);

            var savedStamp = GetFileStamp();
            _cachedLastWriteTicks = savedStamp.LastWriteTicks;
            _cachedLength = savedStamp.Length;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch { }
        }
    }
}
