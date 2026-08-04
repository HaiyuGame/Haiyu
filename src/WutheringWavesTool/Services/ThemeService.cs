using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Waves.Settings;

namespace Haiyu.Services;

public sealed class ThemeService : IThemeService
{
    private Lazy<string?> _currentTheme;

    public ThemeService(IAppContext<App> appContext,AppSettings appSettings)
    {
        AppContext = appContext;
        AppSettings = appSettings;
        _currentTheme = new(() => AppSettings.GetElementThemeAsync().GetAwaiter().GetResult());
    }

    public IAppContext<App> AppContext { get; }
    public AppSettings AppSettings { get; }

    public ElementTheme CurrentTheme
    {
        get
        {
            switch (_currentTheme.Value)
            {
                case "Light":
                    return ElementTheme.Light;
                case "Dark":
                    return ElementTheme.Dark;
                case "Default":
                    return ElementTheme.Default;
                default:
                    return ElementTheme.Default;
            }
        }
    }

    public void SetTheme(ElementTheme? theme = null)
    {
        if(AppContext.App.MainWindow.Content is Page page)
        {
            if (theme == null)
            {
                page.RequestedTheme = ElementTheme.Default;
            }else
                page.RequestedTheme = theme.Value;
        }

        _currentTheme = new(() => AppSettings.GetElementThemeAsync().GetAwaiter().GetResult());
    }
}
