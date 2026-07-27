using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Core;

namespace Haiyu.ViewModel;

partial class SettingViewModel
{

    [ObservableProperty]
    public partial List<string> Themes { get; set; } = ["Default", "Light", "Dark"];

    [ObservableProperty]
    public partial string SelectTheme { get; set; }


    
    partial void OnSelectThemeChanged(string value)
    {
        _ = OnSelectThemeChangedAsync(value);
    }

    private async Task OnSelectThemeChangedAsync(string value)
    {
        if (await AppSettings.GetElementThemeAsync() == value)
        {
            return;
        }
        ThemeService.SetTheme(
            value == "Light" ? ElementTheme.Light
            : value == "Dark" ? ElementTheme.Dark
            : ElementTheme.Default
        );
        await AppSettings.SetElementThemeAsync(value.ToString());
    }

    [RelayCommand]
    async Task ShowGameEnhancedDialog()
    {
        await DialogManager.ShowGameEnhancedDialogAsync();
    }

    [ObservableProperty]
    public partial WallpaperType SelectWallpaperName { get; set; }

    [ObservableProperty]
    public partial List<WallpaperType> WallpaperTypes { get; set; } = [new(LanguageService.GetStringByText("视频")), new(LanguageService.GetStringByText("图片"))];

    partial void OnSelectWallpaperNameChanged(WallpaperType value)
    {
        _ = OnSelectWallpaperNameChangedAsync(value);
    }

    private async Task OnSelectWallpaperNameChangedAsync(WallpaperType value)
    {
        if (value == null)
            return;
        if (value.Name == LanguageService.GetStringByText("视频"))
        {
            await AppSettings.SetWallpaperTypeAsync("Video");
        }
        else
        {
            await AppSettings.SetWallpaperTypeAsync("Image");
        }
    }
}

public class WallpaperType
{
    public string Name { get; set; }

    public WallpaperType(string name)
    {
        Name = name;
    }
}
