
using MemoryPack;
using Waves.Core.Settings;

namespace Haiyu.Services;

public class PlayerCardService : IPlayerCardService
{
    public async Task<RecordCacheDetily> GetRecordAsync(string name)
    {
        foreach (
               var item in Directory.GetFiles(AppSettings.RecordFolder, "*.json", SearchOption.TopDirectoryOnly)
           )
        {
            try
            {
                var data = MemoryPackSerializer.Deserialize<RecordCacheDetily>(
                    await File.ReadAllBytesAsync(item),
                    new MemoryPackSerializerOptions() { StringEncoding = StringEncoding.Utf8 }
                );
                if (data.Name == name)
                    return data;
            }
            catch (Exception)
            {
                continue;
            }
        }
        return null;
    }

    public async Task<List<RecordCacheDetily>> GetRecordsAsync()
    {
        List<RecordCacheDetily> values = [];
        foreach (
               var item in Directory.GetFiles(AppSettings.RecordFolder, "*.json", SearchOption.TopDirectoryOnly)
           )
        {
            try
            {
                var data = MemoryPackSerializer.Deserialize<RecordCacheDetily>(
                    await File.ReadAllBytesAsync(item),
                    new MemoryPackSerializerOptions() { StringEncoding = StringEncoding.Utf8 }
                );
                values.Add(data);
            }
            catch (Exception)
            {
                continue;
            }
        }
        return values;
    }
}
