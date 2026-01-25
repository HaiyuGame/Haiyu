using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Waves.Core.Models;

namespace Waves.Core.Common;

public class SettingBase
{
    public SettingBase(string configPath)
    {
        this.configPath = configPath;
        _settingsCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _lockObj = new object();
    }
    private Dictionary<string, string> _settingsCache;
    private readonly string configPath;
    private bool _isLoaded = false;
    private readonly object _lockObj;

    internal virtual string? Read([CallerMemberName] string key = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        LoadSettingsOnce();
        try
        {
            lock (_lockObj)
            {
                _settingsCache.TryGetValue(key, out var value);
                return value;
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal virtual void Write(string? value, [CallerMemberName] string key = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("配置键名不能为空", nameof(key));
        }
        LoadSettingsOnce();

        lock (_lockObj)
        {
            if (value == null)
            {
                _settingsCache.Remove(key);
            }
            else
            {
                _settingsCache[key] = value;
            }
            SaveSettings();
        }
    }

    private void SaveSettings()
    {
        try
        {
            lock (_lockObj)
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
                File.WriteAllText(configPath, json);
            }
        }
        catch (Exception ex)
        {
            throw new IOException("配置文件写入失败", ex);
        }
    }

    private void LoadSettingsOnce()
    {
        DoLoadSettings();
    }

    private void DoLoadSettings()
    {
        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            try
            {
                var settingsList = JsonSerializer.Deserialize<List<LocalSettings>>(
                    json,
                    LocalSettingsJsonContext.Default.ListLocalSettings
                );
                if (settingsList != null && settingsList.Count > 0)
                {
                    _settingsCache = settingsList.ToDictionary(
                        x => x.Key,
                        x => x.Value,
                        StringComparer.OrdinalIgnoreCase
                    );
                }
            }
            catch (Exception)
            {
                
                _settingsCache = new Dictionary<string, string>();
            }
        }
        else
        {
            _settingsCache = new Dictionary<string, string>();
        }
    }

    public void LoadSettings()
    {
        lock (_lockObj)
        {
            DoLoadSettings();
            _isLoaded = true;
        }
    }
}
