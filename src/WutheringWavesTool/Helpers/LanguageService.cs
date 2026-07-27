using LanguageEditer.Model;
using Microsoft.Windows.Globalization;
using Waves.Core.Settings;

namespace Haiyu.Helpers;

public static class LanguageService
{
    public static IReadOnlyCollection<string> Languages => ["en-us","zh-Hans","zh-Hant","ja-jp"];

    public static AppSettings AppSettings { get; private set; }

    private static Dictionary<string, string> Zh_Hans  = [];
    private static Dictionary<string, string> Zh_Hant = [];
    private static Dictionary<string, string> En_Us = [];
    private static Dictionary<string, string> Ja_Jp = [];
    
    static LanguageService()
    {
        AppSettings = Instance.Host.Services.GetRequiredService<AppSettings>();
    }

    public static string GetLanguage()
    {
        return AppSettings.GetLanguageAsync().GetAwaiter().GetResult() ?? "";
    }

    public static async Task InitAsync()
    {
        try
        {
            Zh_Hans = JsonSerializer.Deserialize(await File.ReadAllTextAsync(AppDomain.CurrentDomain.BaseDirectory+"\\Assets\\Languages\\zh-Hans.json"),ProjectLanguageModelContext.Default.ListLanguageItem)?.ToDictionary(x=>x.Key,x=>x.Value)??[];
            Zh_Hant = JsonSerializer.Deserialize(await File.ReadAllTextAsync(AppDomain.CurrentDomain.BaseDirectory+ "\\Assets\\Languages\\zh-Hant.json"), ProjectLanguageModelContext.Default.ListLanguageItem)?.ToDictionary(x => x.Key, x => x.Value) ?? [];
            En_Us = JsonSerializer.Deserialize(await File.ReadAllTextAsync(AppDomain.CurrentDomain.BaseDirectory+ "\\Assets\\Languages\\en-US.json"), ProjectLanguageModelContext.Default.ListLanguageItem)?.ToDictionary(x => x.Key, x => x.Value) ?? [];
            Ja_Jp = JsonSerializer.Deserialize(await File.ReadAllTextAsync(AppDomain.CurrentDomain.BaseDirectory+ "\\Assets\\Languages\\ja-JP.json"), ProjectLanguageModelContext.Default.ListLanguageItem)?.ToDictionary(x => x.Key, x => x.Value) ?? [];
        }
        catch (Exception)
        {

            throw;
        }
    }

    public static string? GetString(string key)
    {
        var language = AppSettings.GetLanguageAsync().GetAwaiter().GetResult();
        string result = "";
        if(language == "en-us" && En_Us.TryGetValue(key,out result))
        {
            return result;
        }
        if(language == "zh-Hans" && Zh_Hans.TryGetValue(key, out result))
        {
            return result;
        }
        if(language == "zh-Hant" && Zh_Hant.TryGetValue(key, out result))
        {

            return result;
        }
        if(language == "ja-jp" && Ja_Jp.TryGetValue(key, out result))
        {
            return result;
        }
        // Keep newly added UI resources readable while a locale is being completed in
        // LanguageEditer.  A missing translated entry should never render as an empty
        // control in the application.
        return Zh_Hans.TryGetValue(key, out result) ? result : key;
    }

    public static bool SetLanguage(string language)
    {
        AppSettings.SetLanguageAsync(language).GetAwaiter().GetResult();
        return true;
    }
}
