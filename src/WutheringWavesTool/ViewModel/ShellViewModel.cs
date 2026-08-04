using Haiyu.Pages.GamePages;
using Haiyu.Services.DialogServices;
using Haiyu.ViewModel.GameViewModels;
using Haiyu.ViewModel.GameViewModels.GameContexts;
using Waves.Api.Models.KuroClient;
using Waves.Api.Models.KuroClient.Options;
using Waves.Core.Models.Enums;
using Waves.Core.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Security.Credentials.UI;

namespace Haiyu.ViewModel;

public sealed partial class ShellViewModel : ViewModelBase
{
    private bool computerShow;
    private CancellationTokenSource? _messageCts = new();

    public ShellViewModel(
        [FromKeyedServices(nameof(HomeNavigationService))] INavigationService homeNavigationService,
        [FromKeyedServices(nameof(HomeNavigationViewService))]
            INavigationViewService homeNavigationViewService,
        ITipShow tipShow,
        IAppContext<App> appContext,
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager,
        IViewFactorys viewFactorys,
        IWallpaperService wallpaperService,
        IKuroClient kuroClient,
        IKuroAccountService kuroAccountService,
        SystemEventPublisher systemEventPublisher,
        ITaskManager taskManager
    )
    {
        HomeNavigationService = homeNavigationService;
        HomeNavigationViewService = homeNavigationViewService;
        TipShow = tipShow;
        AppContext = appContext;
        DialogManager = dialogManager;
        ViewFactorys = viewFactorys;
        WallpaperService = wallpaperService;
        KuroClient = kuroClient;
        KuroAccountService = kuroAccountService;
        SystemEventPublisher = systemEventPublisher;
        TaskManager = taskManager;
        RegisterMessanger();
        SystemMenu = new NotifyIconMenu()
        {
            Items = new List<NotifyIconMenuItem>()
            {
                new()
                {
                    Header = LanguageService.GetStringByText("显示主界面"),
                    Command = this.ShowWindowCommand,
                },
                new()
                {
                    Header = LanguageService.GetStringByText("退出启动器"),
                    Command = this.ExitWindowCommand,
                },
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
    public IWallpaperService WallpaperService { get; }
    public IKuroClient KuroClient { get; }
    public IKuroAccountService KuroAccountService { get; }
    public SystemEventPublisher SystemEventPublisher { get; }
    public ITaskManager TaskManager { get; }

    [ObservableProperty]
    public partial string ServerName { get; set; }

    [ObservableProperty]
    public partial object SelectItem { get; set; }

    [ObservableProperty]
    public partial Visibility SwitchGame { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility LoginBthVisibility { get; set; } = Visibility.Collapsed;

    [RelayCommand]
    public void ClearMemory()
    {
        GC.Collect();
    }

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
    public partial string HeaderCover { get; set; } =
        "https://prod-alicdn-community.kurobbs.com/newHead/aki/yangyang.png?x-oss-process=image/resize,w_240,h_240";

    [ObservableProperty]
    public partial string HeaderUserName { get; set; }

    [ObservableProperty]
    public partial CollectionViewSource RoleViewSource { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<SystemMessagerModel> Messages { get; set; } = new();

    [RelayCommand]
    void RefreshCurrentPage()
    {
        WeakReferenceMessenger.Default.Send<RefreshGamePageMessager>(new(true));
    }

    private void RegisterMessanger()
    {
        this.Messenger.Register<SelectUserMessanger>(this, LoginMessangerMethod);
        this.Messenger.Register<CopyTokenAccount>(this, CopyTokenMethod);
        this.Messenger.Register<CopyDeviceDidAccount>(this, CopyDeviceDidMethod);
        this.Messenger.Register<CopyUserIdAccount>(this, CopyUserIdMethod);
        this.Messenger.Register<SystemMessageClose>(this, CloseMessage);
    }

    private void CloseMessage(object recipient, SystemMessageClose message)
    {
        this.Messages.Remove(message.Message);
    }

    private async void CopyTokenMethod(object recipient, CopyTokenAccount message)
    {
        var result = await UserConsentVerifier.RequestVerificationAsync(
            LanguageService.GetStringByText("复制授权码需要系统用户密码")
        );
        if (result != UserConsentVerificationResult.Verified)
        {
            TipShow.ShowMessage(
                LanguageService.GetStringByText("系统用户验证失败！"),
                Symbol.Clear
            );
            return;
        }

        DataPackage package = new();
        package.SetText(message.accountToken);
        Clipboard.SetContent(package);
    }

    private async void CopyDeviceDidMethod(object recipient, CopyDeviceDidAccount message)
    {
        DataPackage package = new();
        package.SetText(message.deviceDid);
        Clipboard.SetContent(package);
    }

    private async void CopyUserIdMethod(object recipient, CopyUserIdAccount message)
    {
        DataPackage package = new();
        package.SetText(message.userId);
        Clipboard.SetContent(package);
    }

    [RelayCommand]
    async Task OpenMain()
    {
        var launcheArgs = await AppSettings.GetLauncheBthAsync();
        switch (launcheArgs)
        {
            case "Home":
                this.HomeNavigationService.NavigationTo<HomeViewModel>(
                    null,
                    new DrillInNavigationTransitionInfo()
                );
                break;
            case "WutheringWaves":
                this.HomeNavigationService.NavigationTo<WavesV2GameContextViewModel>(
                    null,
                    new DrillInNavigationTransitionInfo()
                );
                break;
            case "PunishingGrayRaven":
                this.HomeNavigationService.NavigationTo<PunishV2GameContextViewModel>(
                    null,
                    new DrillInNavigationTransitionInfo()
                );
                break;
            case "CloudWutheringWaves":
                this.HomeNavigationService.NavigationTo<WavesCloudGameViewModel>(
                    null,
                    new DrillInNavigationTransitionInfo()
                );
                break;
            default:
                break;
        }
        ;
    }

    [RelayCommand]
    void ClearMessage()
    {
        this.Messages.Clear();
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
        //WindowAllowTransparentBase base1 = new WindowAllowTransparentBase();
        //base1.Manager.MaxHeight = 200;
        //base1.Manager.MaxWidth = 300;
        //base1.Content = new XboxDisplayPage();
        //base1.Show();
    }

    [RelayCommand]
    async Task OpenScreenCapture()
    {
        var result = await DialogManager.GetQRLoginResultAsync();
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
        if (KuroAccountService.CurrentAccount is null)
            return;
        var account = KuroAccountService.CurrentAccount;
        if (long.TryParse(account.UserId, out var _id))
        {
            var result = await KuroClient.GetWavesMineAsync(account, _id, this.CTS.Token);
            if (result == null)
            {
                TipShow.ShowMessage(
                    LanguageService.GetStringByText("检查一下你的网络"),
                    Symbol.Clear
                );
                return;
            }
            if (!result.Success)
            {
                TipShow.ShowMessage(result.Msg, Symbol.Clear);
                return;
            }
            HeaderUserName = result.Data.Mine.UserName;
            HeaderCover = result.Data.Mine.HeadUrl;
            GamerRoleListsVisibility = Visibility.Visible;
        }
        this.AppContext.MainTitle.UpDate();
    }

    [RelayCommand]
    async Task Loaded()
    {
        var account = KuroAccountService.CurrentAccount;
        var result = account is not null && await KuroClient.IsLoginAsync(account, this.CTS.Token);
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
        await this.KuroAccountService.SetAutoUser();
        await RefreshHeaderUser();
        await OpenMain();
        await AppContext.UpdateAppAsync();
        await SystemEventPublisher.SubscribeAsync(OnMessageChanged);
        _ = Task.Run(async () => await TaskManager.InitializeAutoLaunchTasksAsync());
    }

    private async ValueTask OnMessageChanged(SystemMessagerModel model)
    {
        await this.AppContext.TryInvokeAsync(async () =>
        {
            this.Messages.Add(model);
            if (Messages.Count > 50)
                Messages.RemoveAt(0);
            await Task.CompletedTask;
        });

        if (model.Delay > 0)
        {
            var ct = _messageCts?.Token ?? CancellationToken.None;
            _ = AutoRemoveAsync(model, TimeSpan.FromSeconds(model.Delay), ct);
        }
    }

    private async Task AutoRemoveAsync(
        SystemMessagerModel model,
        TimeSpan delay,
        CancellationToken ct
    )
    {
        try
        {
            await Task.Delay(delay, ct);
            await AppContext.TryInvokeAsync(async () => Messages.Remove(model));
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    public void ShowDeviceInfo()
    {
        var window = ViewFactorys.ShowAdminDevice();
        window.Activate();
    }

    internal void SetSelectItem(Type sourcePageType)
    {
        var page = this.HomeNavigationViewService.GetSelectItem(sourcePageType);
        SelectItem = page;
    }

    [ObservableProperty]
    public partial double EncourageTotalGold { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<EncourageDailyTask> EncourageDailys { get; set; }

    [RelayCommand]
    public async Task EncourageDataAsync()
    {
        try
        {
            if (KuroAccountService.CurrentAccount == null)
                return;

            var encourageProcess = await this.TryInvokeAsync(async () =>
                await this.KuroClient.GetEncourageProcessAsync(
                    KuroAccountService.CurrentAccount,
                    EncourageProcessOption.CreateDefault(KuroAccountService.CurrentAccount.UserId),
                    this.CTS.Token
                )
            );
            var encourageTotal = await this.TryInvokeAsync(async () =>
                await this.KuroClient.GetEncourageTotalGoldAsync(
                    KuroAccountService.CurrentAccount,
                    this.CTS.Token
                )
            );
            if (encourageProcess.Code != 0 || encourageTotal.Code != 0)
            {
                SystemEventMessager.Publish(
                    new SystemMessagerModel()
                    {
                        Message = LanguageService.GetString("Code_KuroCoinFetchFailed")!,
                        Delay = TimeSpan.FromSeconds(3).TotalSeconds,
                    }
                );
                return;
            }
            this.EncourageTotalGold = encourageTotal.Result!.Data.GoldNum;
            this.EncourageDailys = encourageProcess.Result!.Data.DailyTask.ToObservableCollection();
        }
        catch (Exception ex)
        {
            SystemEventMessager.Publish(
                    new SystemMessagerModel()
                    {
                        Message = LanguageService.GetString("Code_KuroCoinFetchFailed")!,
                        Delay = TimeSpan.FromSeconds(3).TotalSeconds,
                    }
                );
            this.Logger.WriteError(ex.Message + ex.StackTrace);
            return;
        }
    }
}
