using System.Text;
using Waves.Core.Settings;

namespace Haiyu.Services;

public sealed partial class RecordCacheService : IRecordCacheService
{
    public async Task<bool> CreateRecordAsync(RecordCacheDetily? recordCacheDetily)
    {
        if (recordCacheDetily == null)
            return false;
        var filePath = $"{AppSettings.RecordFolder}\\{recordCacheDetily.Name}.json";
        if (File.Exists($"{AppSettings.RecordFolder}"))
        {
            File.Delete(filePath);
        }
        using (var fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.ReadWrite))
        {
            using (var writer = new StreamWriter(fs, Encoding.UTF8))
            {
                try
                {
                    var json = JsonSerializer.Serialize(
                        recordCacheDetily,
                        PlayerCardRecordContext.Default.RecordCacheDetily
                    );
                    await writer.WriteAsync(json);
                    return true;
                }
                catch (System.Exception)
                {
                    await writer.FlushAsync();
                    writer.Close();
                    File.Delete(filePath);
                    return false;
                }
            }
        }
    }

    public async Task<IEnumerable<RecordCacheDetily?>> GetRecordCacheDetilyAsync()
    {
        List<RecordCacheDetily?> list = new List<RecordCacheDetily?>();
        var dirinfo = new DirectoryInfo(AppSettings.RecordFolder);
        foreach (var filePath in dirinfo.EnumerateFiles("*.json"))
        {
            using (var fs = new FileStream(filePath.FullName, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new StreamReader(fs, System.Text.Encoding.UTF8))
                {
                    var str = await reader.ReadToEndAsync();
                    try
                    {
                        var json = JsonSerializer.Deserialize<RecordCacheDetily>(
                            str,
                            PlayerCardRecordContext.Default.RecordCacheDetily
                        );
                        list.Add(json);
                    }
                    catch (System.Exception)
                    {
                        reader.Close();
                        continue;
                    }
                }
            }
        }
        return list;
    }

    public async Task<IEnumerable<(RecordCacheDetily?, string?)>> GetRecordCacheDetilyAndPathAsync()
    {
        List<(RecordCacheDetily?, string?)> list = new List<(RecordCacheDetily?, string?)>();
        var dirinfo = new DirectoryInfo(AppSettings.RecordFolder);
        foreach (var filePath in dirinfo.EnumerateFiles("*.json"))
        {
            using (var fs = new FileStream(filePath.FullName, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new StreamReader(fs, System.Text.Encoding.UTF8))
                {
                    var str = await reader.ReadToEndAsync();
                    try
                    {
                        var json = JsonSerializer.Deserialize<RecordCacheDetily>(
                            str,
                            PlayerCardRecordContext.Default.RecordCacheDetily
                        );
                        list.Add((json, filePath.FullName));
                    }
                    catch (System.Exception)
                    {
                        reader.Close();
                        continue;
                    }
                }
            }
        }
        return list;
    }
}
