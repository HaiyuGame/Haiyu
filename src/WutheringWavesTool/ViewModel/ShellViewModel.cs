using Haiyu.Common.WindowContext;
using Haiyu.Pages.GamePages;
using Haiyu.ViewModel.GameViewModels;
using Haiyu.ViewModel.GameViewModels.GameContexts;
using Microsoft.UI.Xaml.Input;
using Waves.Api.Models.KuroClient;
using Waves.Api.Models.KuroClient.Options;
using Waves.Core.Contracts.Events;
using Waves.Core.Models.Enums;
using Waves.Core.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Security.Credentials.UI;

namespace Haiyu.ViewModel;

public sealed partial class ShellViewModel : ViewModelBase
{
    private bool computerShow;
    private CancellationTokenSource? _messageCts = new();
    private IGameEventSubscription? _systemEventSubscription;

    public ShellViewModel(
        IAppContext<App> appContext,
        IWindowManager windowManager,
        IViewFactorys viewFactorys,
        IWallpaperService wallpaperService,
        IKuroClient kuroClient,
        IKuroAccountService kuroAccountService,
        SystemEventPublisher systemEventPublisher,
        ITaskManager taskManager
    )
    {
        AppContext = appContext;
        this.WindowManager = windowManager;
        ViewFactorys = viewFactorys;
        WallpaperService = wallpaperService;
        KuroClient = kuroClient;
        KuroAccountService = kuroAccountService;
        SystemEventPublisher = systemEventPublisher;
        TaskManager = taskManager;
        RegisterMessanger();
    }

    [ObservableProperty]
    public partial NotifyIconMenu SystemMenu { get; set; }

    public IAppContext<App> AppContext { get; }
    public IViewFactorys ViewFactorys { get; }
    public IWallpaperService WallpaperService { get; }

    public IWindowManager WindowManager { get; }
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
        //var result = await UserConsentVerifier.RequestVerificationAsync(
        //    LanguageService.GetStringByText("复制授权码需要系统用户密码")
        //);
        //if (result != UserConsentVerificationResult.Verified)
        //{
        //    TipShow.ShowMessage(
        //        LanguageService.GetStringByText("系统用户验证失败！"),
        //        Symbol.Clear
        //    );
        //    return;
        //}

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
                this.WindowManager.Shell.NavigationService.NavigationTo<HomeViewModel>(
                    null,
                    new DrillInNavigationTransitionInfo()
                );
                break;
            case "WutheringWaves":
                this.WindowManager.Shell.NavigationService.NavigationTo<WavesV2GameContextViewModel>(
                    null,
                    new DrillInNavigationTransitionInfo()
                );
                break;
            case "PunishingGrayRaven":
                this.WindowManager.Shell.NavigationService.NavigationTo<PunishV2GameContextViewModel>(
                    null,
                    new DrillInNavigationTransitionInfo()
                );
                break;
            case "CloudWutheringWaves":
                this.WindowManager.Shell.NavigationService.NavigationTo<WavesCloudGameViewModel>(
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
        await WindowManager.Shell.DialogManager.ShowLocalUserManagerAsync();
    }

    [RelayCommand]
    void BackPage()
    {
        if (this.WindowManager.Shell.NavigationService.CanGoBack)
            this.WindowManager.Shell.NavigationService.GoBack();
    }

    [RelayCommand]
    void Min()
    {
        this.WindowManager.Shell.Minimize();
    }

    [RelayCommand]
    async Task CloseWindow()
    {
        var close = await AppSettings.GetCloseWindowAsync();
        if (close == "True")
        {
            Environment.Exit(0);
        }
        else if (close == "False")
        {
            this.AppContext.WindowManager.Shell.Hide();
        }
        else
        {
            var result = await AppContext.WindowManager.Shell.DialogManager.ShowCloseWindowResult();
            if (result.IsExit)
            {
                Environment.Exit(0);
            }
            else
            {
                this.AppContext.WindowManager.Shell.Hide();
            }
        }
    }

    [RelayCommand]
    void ShowWindow()
    {
        this.AppContext.WindowManager.Shell.Show();
    }

    [RelayCommand]
    void ExitWindow()
    {
        Environment.Exit(0);
    }

    [RelayCommand]
    void OpenSetting()
    {
        this.WindowManager.Shell.NavigationService.NavigationTo<SettingViewModel>(
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
        var result = await WindowManager.Shell.DialogManager.GetQRLoginResultAsync();
    }

    [RelayCommand]
    async Task Login()
    {
        await WindowManager.Shell.DialogManager.ShowLoginDialogAsync();
    }

    [RelayCommand]
    async Task LoginWebGame()
    {
        await WindowManager.Shell.DialogManager.ShowWebGameDialogAsync();
    }

    private async void LoginMessangerMethod(object recipient, SelectUserMessanger message)
    {
        this.LoginBthVisibility = Visibility.Collapsed;
        WavesCommunitySelectItemVisiblity = Visibility.Visible;
        await RefreshHeaderUser();
        await Task.Delay(800);
        if (this.WindowManager.Shell is ShellWindowContext shell)
        {
            shell.MainTitle.UpDate();
        }
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
                WindowManager.Shell.TipShow.ShowMessage(
                    LanguageService.GetStringByText("检查一下你的网络"),
                    Symbol.Clear
                );
                return;
            }
            if (!result.Success)
            {
                WindowManager.Shell.TipShow.ShowMessage(result.Msg, Symbol.Clear);
                return;
            }
            HeaderUserName = result.Data.Mine.UserName;
            HeaderCover = result.Data.Mine.HeadUrl;
            GamerRoleListsVisibility = Visibility.Visible;
        }
        if (this.WindowManager.Shell is ShellWindowContext shell)
        {
            shell.MainTitle.UpDate();
        }
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
        if (this.WindowManager.Shell is ShellWindowContext shell)
        {
            shell.MainTitle.UpDate();
        }
        await this.KuroAccountService.SetAutoUser();
        await RefreshHeaderUser();
        await OpenMain();
        await AppContext.UpdateAppAsync();
        var subscription = await SystemEventPublisher.SubscribeAsync(OnMessageChanged);
        if (IsDisposed)
        {
            subscription.Dispose();
            return;
        }

        _systemEventSubscription = subscription;
        _ = Task.Run(async () => await TaskManager.InitializeAutoLaunchTasksAsync());
    }

    private async ValueTask OnMessageChanged(SystemMessagerModel model)
    {
        await this.WindowManager.Shell.TryInvokeAsync(async () =>
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
            await WindowManager.Shell.TryInvokeAsync(async () => Messages.Remove(model));
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    public void ShowDeviceInfo()
    {
        ViewFactorys.ShowAdminDevice();
    }

    internal void SetSelectItem(Type sourcePageType)
    {
        var page = WindowManager.Shell.NavigationViewService.GetSelectItem(sourcePageType);
        SelectItem = page;
    }

    [ObservableProperty]
    public partial double EncourageTotalGold { get; set; }

    [RelayCommand]
    public async Task OpenDevShellDialog(KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.F12)
        {
            await this.WindowManager.Shell.DialogManager.ShowGrayConfigAsync();
        }
    }

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

    protected override void OnDisposing()
    {
        _systemEventSubscription?.Dispose();
        _systemEventSubscription = null;

        var messageCts = Interlocked.Exchange(ref _messageCts, null);
        if (messageCts is not null)
        {
            try
            {
                messageCts.Cancel();
            }
            catch (ObjectDisposedException) { }

            messageCts.Dispose();
        }

        base.OnDisposing();
    }
}
