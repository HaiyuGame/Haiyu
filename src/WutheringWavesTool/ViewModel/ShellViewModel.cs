using System.Linq;
using Astronomical;
using Haiyu.Models.Wrapper;
using Haiyu.Services.DialogServices;
using Waves.Core.Common;
using Waves.Core.Models.Enums;
using Windows.Devices.Geolocation;
using Windows.Graphics.DirectX.Direct3D11;

namespace Haiyu.ViewModel;

public sealed partial class ShellViewModel : ViewModelBase
{
    private bool computerShow;

    public ShellViewModel(
        [FromKeyedServices(nameof(HomeNavigationService))] INavigationService homeNavigationService,
        [FromKeyedServices(nameof(HomeNavigationViewService))]
            INavigationViewService homeNavigationViewService,
        ITipShow tipShow,
        IAppContext<App> appContext,
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager,
        IViewFactorys viewFactorys,
        IKuroClient wavesClient,
        IWallpaperService wallpaperService,IKuroClient kuroClient
    )
    {
        HomeNavigationService = homeNavigationService;
        HomeNavigationViewService = homeNavigationViewService;
        TipShow = tipShow;
        AppContext = appContext;
        DialogManager = dialogManager;
        ViewFactorys = viewFactorys;
        WavesClient = wavesClient;
        WallpaperService = wallpaperService;
        KuroClient = kuroClient;
        RegisterMessanger();
        SystemMenu = new NotifyIconMenu()
        {
            Items = new List<NotifyIconMenuItem>()
            {
                new() { Header = "显示主界面", Command = this.ShowWindowCommand },
                new() { Header = "退出启动器", Command = this.ExitWindowCommand },
            },
        };
    }

    [ObservableProperty]
    public partial NotifyIconMenu SystemMenu { get; set; }

    public INavigationService HomeNavigationService { get; }
    public INavigationViewService HomeNavigationViewService { get; }
    public ITipShow TipShow { get; }
    public IAppContext<App> AppContext { get; }
    public IDialogManager DialogManager { get; }
    public IViewFactorys ViewFactorys { get; }
    public IKuroClient WavesClient { get; }
    public IWallpaperService WallpaperService { get; }
    public IKuroClient KuroClient { get; }
    [ObservableProperty]
    public partial string ServerName { get; set; }

    [ObservableProperty]
    public partial object SelectItem { get; set; }

    [ObservableProperty]
    public partial Visibility LoginBthVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility GamerRoleListsVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility WavesCommunitySelectItemVisiblity { get; set; } =
        Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility PunishCommunitySelectItemVisiblity { get; set; } =
        Visibility.Collapsed;

    public Controls.ImageEx Image { get; set; }
    public Border BackControl { get; internal set; }

    [ObservableProperty]
    public partial string HeaderCover { get; set; } = "https://prod-alicdn-community.kurobbs.com/newHead/aki/yangyang.png?x-oss-process=image/resize,w_240,h_240";

    [ObservableProperty]
    public partial string HeaderUserName { get; set; }

    [ObservableProperty]
    public partial CollectionViewSource RoleViewSource { get; set; }


    private void RegisterMessanger()
    {
        this.Messenger.Register<SelectUserMessanger>(this, LoginMessangerMethod);
    }

   

    [RelayCommand]
    void OpenMain()
    {
        this.HomeNavigationService.NavigationTo<HomeViewModel>(
            null,
            new DrillInNavigationTransitionInfo()
        );
    }

    [RelayCommand]
    void OpenColorGame()
    {
        var result = this.ViewFactorys.ShowColorGame();
        result.Manager.MaxHeight = 600;
        result.Manager.MaxWidth = 1000;
        result.Manager.Height = 600;
        result.Manager.Width = 1000;
        result.Activate();
    }

    [RelayCommand]
    void OpenStartColorGame()
    {
        var result = this.ViewFactorys.ShowStartColorGame();
        result.Manager.MaxHeight = 600;
        result.Manager.MaxWidth = 1000;
        result.Manager.Height = 600;
        result.Manager.Width = 1000;
        result.Activate();
    }

    [RelayCommand]
    async Task ShowOpenLocalUser()
    {
        await DialogManager.ShowLocalUserManagerAsync();
    }

    [RelayCommand]
    void BackPage()
    {
        if (HomeNavigationService.CanGoBack)
            HomeNavigationService.GoBack();
    }

    [RelayCommand]
    void Min()
    {
        this.AppContext.Minimise();
    }

    [RelayCommand]
    void CloseWindow()
    {
        this.AppContext.CloseAsync();
    }

    [RelayCommand]
    void ShowWindow()
    {
        this.AppContext.App.MainWindow.Show();
    }

    [RelayCommand]
    void ExitWindow()
    {
        Environment.Exit(0);
    }


    [RelayCommand]
    void OpenSetting()
    {
        this.HomeNavigationService.NavigationTo<SettingViewModel>(
            "Setting",
            new DrillInNavigationTransitionInfo()
        );
    }

    [RelayCommand]
    async Task OpenScreenCapture()
    {
        var result = await DialogManager.GetQRLoginResultAsync();
    }

    [RelayCommand]
    void OpenTest()
    {
        this.HomeNavigationService.NavigationTo<TestViewModel>(
            "Setting",
            new DrillInNavigationTransitionInfo()
        );
    }

    [RelayCommand]
    void OpenPlayerRecordWindow()
    {
        var win = ViewFactorys.ShowPlayerRecordWindow();
        (win.AppWindow.Presenter as OverlappedPresenter)!.IsMaximizable = false;
        (win.AppWindow.Presenter as OverlappedPresenter)!.IsMinimizable = false;
        win.SystemBackdrop = new MicaBackdrop();
        win.Activate();
    }

    [RelayCommand]
    async Task Login()
    {
        await DialogManager.ShowLoginDialogAsync();
    }

    [RelayCommand]
    async Task LoginWebGame()
    {
        await DialogManager.ShowWebGameDialogAsync();
    }

    private async void LoginMessangerMethod(object recipient, SelectUserMessanger message)
    {
        this.LoginBthVisibility = Visibility.Collapsed;
        WavesCommunitySelectItemVisiblity = Visibility.Visible;
        await RefreshHeaderUser();
        await Task.Delay(800);
        this.AppContext.MainTitle.UpDate();
    }

    [RelayCommand]
    public async Task RefreshHeaderUser()
    {
        if (KuroClient.AccountService.Current == null)
            return;
        var current = KuroClient.AccountService.Current;
        if(long.TryParse(current.TokenId,out var _id))
        {
            var result = await KuroClient.GetWavesMineAsync(_id, current.TokenId, current.Token, this.CTS.Token);
            if(result == null)
            {
                TipShow.ShowMessage("检查一下你的网络", Symbol.Clear);
                return;
            }
            if (!result.Success)
            {
                TipShow.ShowMessage(result.Msg, Symbol.Clear);
                return;
            }
            HeaderUserName = result.Data.Mine.UserName;
            HeaderCover = result.Data.Mine.HeadUrl;
        }
        this.AppContext.MainTitle.UpDate();
    }

    [RelayCommand]
    async Task Loaded()
    {
        var network = await NetworkCheck.PingAsync(GameAPIConfig.BaseAddress[0]);
        if (network == null || network.Status != System.Net.NetworkInformation.IPStatus.Success)
        {
            Logger.WriteError($"检查库洛CDN服务器失败！，地址为:{GameAPIConfig.BaseAddress[0]}");
            Environment.Exit(0);
        }
        await KuroClient.AccountService.SetAutoUser();
        var result = await WavesClient.IsLoginAsync(this.CTS.Token);
        if (!result)
        {
            this.LoginBthVisibility = Visibility.Visible;
            WavesCommunitySelectItemVisiblity = Visibility.Collapsed;
            PunishCommunitySelectItemVisiblity = Visibility.Collapsed;
        }
        else
        {
            this.LoginBthVisibility = Visibility.Collapsed;
            WavesCommunitySelectItemVisiblity = Visibility.Visible;
            this.GamerRoleListsVisibility = Visibility.Visible;
            await this.RefreshHeaderUser();
        }
        this.AppContext.MainTitle.UpDate();
        WallpaperService.SetMediaForUrl(WallpaperShowType.Image, AppDomain.CurrentDomain.BaseDirectory+ "Assets\\background.png");
        OpenMain();
    }

    [RelayCommand]
    public void ShowDeviceInfo()
    {
        var window = ViewFactorys.ShowAdminDevice();
        window.Activate();
    }

    [RelayCommand]
    async Task UnLogin()
    {
       await Task.CompletedTask;
    }

    [RelayCommand]
    void OpenCounter(RoutedEventArgs args) { }

    internal void SetSelectItem(Type sourcePageType)
    {
        var page = this.HomeNavigationViewService.GetSelectItem(sourcePageType);
        SelectItem = page;
    }

}
