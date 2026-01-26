using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Linq;
using Waves.Core.Models;

namespace Waves.Core.Common;

public class SettingBase
{
    private readonly string _configPath;
    private readonly object _lockObj = new();
    private Dictionary<string, string> _settingsCache;
    private bool _isLoaded = false;

    public SettingBase(string configPath)
    {
        _configPath = configPath;
        _settingsCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

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

                var directory = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_configPath, json);
            }
        }
        catch (Exception ex)
        {
            throw new IOException("配置文件写入失败", ex);
        }
    }
    private void LoadSettingsOnce()
    {
        lock (_lockObj)
        {
            if (!_isLoaded)
            {
                DoLoadSettings();
                _isLoaded = true;
            }
        }
    }

    private void DoLoadSettings()
    {
        lock (_lockObj)
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
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
                    else
                    {
                        _settingsCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch (Exception)
                {
                    _settingsCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
            }
            else
            {
                _settingsCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
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