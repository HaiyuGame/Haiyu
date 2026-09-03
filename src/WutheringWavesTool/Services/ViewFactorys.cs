using Haiyu.Common.Contracts;
using Haiyu.Pages.Communitys;
using Haiyu.Pages.Toolkits;
using Waves.Api.Models.CloudGame;
using Waves.Core.Models.CloudGame;

namespace Haiyu.Services;

public class ViewFactorys : IViewFactorys
{
    private static readonly WindowsOption GeetWindowOption =
        new()
        {
            Width = 700,
            Height = 510,
            MaxWidth = 700,
            MaxHeight = 510,
            IsResizable = false,
            IsMaximizable = false,
            CenterOnScreen = true,
        };

    private static readonly WindowsOption DeviceInfoWindowOption =
        new()
        {
            Width = 750,
            Height = 530,
            MaxWidth = 750,
            MaxHeight = 530,
            IsResizable = false,
            IsMaximizable = false,
            CenterOnScreen = true,
        };

    public ViewFactorys(IAppContext<App> appContext)
    {
        AppContext = appContext;
    }

    public IAppContext<App> AppContext { get; }

    public GetGeetWindow CreateGeetWindow(GeetType type)
    {
        return new GetGeetWindow(
            WindowNative.GetWindowHandle(AppContext.App.MainWindow),
            type,
            GeetWindowOption
        );
    }

    public WindowModelBase ShowSignWindow(GameRoilDataItem role) =>
        this.ShowWindowBase<GamerSignPage>(role);

    public WindowModelBase ShowWindowBase<T>(object? data)
        where T : UIElement, IWindowPage
    {
        var win = new WindowModelBase(WindowNative.GetWindowHandle(AppContext.App.MainWindow));
        var page = Instance.Host.Services!.GetRequiredService<T>();
        if (data != null)
            page.SetData(data);
        page.SetWindow(win);
        win.Content = page;
        return win;
    }

    public WindowModelBase ShowAdminDevice()
    {
        var win = new WindowModelBase(
            WindowNative.GetWindowHandle(AppContext.App.MainWindow),
            DeviceInfoWindowOption
        );
        var page = Instance.Host.Services!.GetRequiredService<DeviceInfoPage>();
        page.SetWindow(win);
        win.Content = page;
        return win;
    }



    public WindowModelBase ShowAnalysisRecordV2(CloudGameLoginSession selectLogin)
    {
        return this.ShowWindowBase<WavesAnalysisRecordPage>(selectLogin);
    }

    public WindowModelBase ShowAutoKruoTokenWindow()
    {
        return this.ShowWindowBase<AutoKuroTokenPage>(null);
    }


    public Window ShowMonitorToolWindow()
    {
        var win = new TransparentWindowBase();
        var page = Instance.Host.Services!.GetRequiredService<MonitorToolPage>();
        page.SetWindow(win);
        win.Content = page;
        return win;
    }
}
