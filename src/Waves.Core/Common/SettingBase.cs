using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Waves.Core.Models;

namespace Waves.Core.Common
{
    public class SettingBase
    {
        public SettingBase(string configPath)
        {
            this.configPath = configPath;
        }

        // 存储所有设置的内存缓存
        private static List<LocalSettings> _settingsCache;
        private readonly string configPath;

        internal virtual string? Read([CallerMemberName] string key = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return null;
                }

                var item = _settingsCache.FirstOrDefault(x => x.Key == key);
                return item?.Value;
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
                throw new IOException("找不到相关Key");
            }

            if (value == null)
            {
                _settingsCache.RemoveAll(x => x.Key == key);
            }
            else
            {
                var existing = _settingsCache.FirstOrDefault(x => x.Key == key);
                if (existing != null)
                {
                    existing.Value = value;
                }
                else
                {
                    _settingsCache.Add(new LocalSettings { Key = key, Value = value });
                }
            }

            SaveSettings();
        }

        private void SaveSettings()
        {
            var json = JsonSerializer.Serialize(
                _settingsCache,
                LocalSettingsJsonContext.Default.ListLocalSettings
            );
            File.WriteAllText(configPath, json);
        }

        public void LoadSettings()
        {
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                try
                {
                    _settingsCache = JsonSerializer.Deserialize<List<LocalSettings>>(
                        json,
                        LocalSettingsJsonContext.Default.ListLocalSettings
                    );
                }
                catch (Exception)
                {
                    _settingsCache = new();
                }

                SaveSettings();
            }
            else
            {
                _settingsCache = new List<LocalSettings>();
            }
        }
    }
}
