using System.Collections.Specialized;
using Haiyu.Common.WindowContext;
using Haiyu.Pages.GamePages;
using Microsoft.UI.Xaml.Hosting;

namespace Haiyu.Pages;

public sealed partial class ShellPage : Page
{
    public ShellPage()
    {
        this.InitializeComponent();
        this.ViewModel =
            Instance.GetService<ShellViewModel>() ?? throw new ArgumentException(LanguageService.GetStringByText("服务注册错误"));
        this.Loaded += ShellPage_Loaded;
        this.ViewModel.HomeNavigationService.Navigated += HomeNavigationService_Navigated;
        this.ViewModel.HomeNavigationService.RegisterView(this.frame);
        this.ViewModel.HomeNavigationViewService.Register(this.navigationView);
        this.ViewModel.WindowManager.Shell.TipShow.Owner = this.panel;
        if(this.ViewModel.WindowManager.Shell is ShellWindowContext shell) 
        {
            shell.MainTitle = this.titlebar;
        }
        this.ViewModel.AppContext.WallpaperService.RegisterMediaHost(mediaControl);
    }

    private void HomeNavigationService_Navigated(object sender, NavigationEventArgs e)
    {
        if (
            e.SourcePageType == typeof(PunishV2GamePage)
            || e.SourcePageType == typeof(WavesV2GamePage)
            ||e.SourcePageType == typeof(WavesCloudGamePage)
        )
        {
            To0.Start();
            this.titlebar.UpDate();
        }
        else
        {
            To8.Start();
            this.titlebar.UpDate();
            this.ViewModel.WallpaperService.PauseVideo();
        }
        ViewModel.SetSelectItem(e.SourcePageType);
        this.ViewModel.HomeNavigationService.ClearHistory();
        GC.Collect();
    }

    public ShellViewModel ViewModel { get; }

    private void ShellPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        this.ViewModel.WindowManager.Shell.DialogManager.RegisterRoot(this.XamlRoot);
        this.notify.RegisterWin(new WindowEx());
        this.notify.CreateTrayIcon(
            AppDomain.CurrentDomain.BaseDirectory + "\\Assets\\appLogo.ico",
            "Haiyu"
        );

    }

    private void ComboBox_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        this.titlebar.UpDate();
    }

    private void notify_LeftDoubleClick(object sender, EventArgs args)
    {
        this.ViewModel.ShowWindowCommand.Execute(null);
    }

    private void OpenMessagePane(object sender, RoutedEventArgs e)
    {
        view.IsPaneOpen = !view.IsPaneOpen;
    }
}
