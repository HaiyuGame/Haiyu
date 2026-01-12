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
        if (AppSettings.ElementTheme != null && AppSettings.ElementTheme == value)
        {
            return;
        }
        ThemeService.SetTheme(
            value == "Light" ? ElementTheme.Light
            : value == "Dark" ? ElementTheme.Dark
            : ElementTheme.Default
        );
        AppSettings.ElementTheme = value.ToString();
    }

    [ObservableProperty]
    public partial WallpaperType SelectWallpaperName { get; set; }

    [ObservableProperty]
    public partial List<WallpaperType> WallpaperTypes { get; set; } = [new("视频"), new("图片")];

    partial void OnSelectWallpaperNameChanged(WallpaperType value)
    {
        if (value == null)
            return;
        if (value.Name == "视频")
        {
            AppSettings.WallpaperType = "Video";
        }
        else
        {
            AppSettings.WallpaperType = "Image";
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
