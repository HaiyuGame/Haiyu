using System.Diagnostics;
using GrayExtension.Exceptions;
using MemoryPack;

namespace GrayExtension;

/// <summary>
/// 灰度启动工具，开启后需要重启应用程序，再程序中默认不加载
/// </summary>
public class GrayOpenService
{
    public AppGrayOptionSetting Setting { get; private set; } = new() { GrayKeyValues = new() };

    private System.Threading.SemaphoreSlim _slim = new(1, 1);

    public string GrayFilePath { get; private set; }

    public async Task InitAsync(string grayFilePath)
    {
        MemoryPackFormatterProvider.Register<AppGrayOptionSetting>();
        this.GrayFilePath = grayFilePath;
        try
        {
            var bytes = await File.ReadAllBytesAsync(grayFilePath);
            var setting = MemoryPackSerializer.Deserialize<AppGrayOptionSetting>(
                bytes,
                AppGrayOptionSetting.Options
            );
            Setting =
                setting
                ?? new AppGrayOptionSetting { GrayKeyValues = new Dictionary<uint, bool>() };
        }
        catch (Exception)
        {
            Setting = new() { GrayKeyValues = new() };
        }
    }

    public bool GetOpen(uint key, bool isThrowError = true)
    {
        if (Setting.GrayKeyValues.TryGetValue(key, out var result))
        {
            return result;
        }
        if (isThrowError)
        {
            throw GrayException.ThrowNoOpen;
        }
        return false;
    }

    public async Task SetDataAsync(uint key, bool value)
    {
        try
        {
            await _slim.WaitAsync();
            Setting.GrayKeyValues[key] = value;
            await Setting.SaveGrayAsync(this.GrayFilePath);
        }
        catch (Exception) { }
        finally
        {
            _slim.Release();
        }
    }
}

[MemoryPackable]
public sealed partial class AppGrayOptionSetting
{
    [MemoryPackIgnore]
    public static MemoryPackSerializerOptions Options { get; } =
        new() { StringEncoding = StringEncoding.Utf8 };

    public Dictionary<uint, bool> GrayKeyValues { get; set; }

    public async Task SaveGrayAsync(string filePath)
    {
        try
        {
            var byteData = MemoryPackSerializer.Serialize(
                typeof(AppGrayOptionSetting),
                this,
                Options
            );
            if (File.Exists(filePath))
                File.Delete(filePath);
            using var fs = new FileStream(
                filePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                4090
            );
            await fs.WriteAsync(byteData);
            await fs.FlushAsync();
        }
        catch (Exception)
        {
            Debug.WriteLine("灰度文件写入失败");
        }
        finally { }
    }
}
