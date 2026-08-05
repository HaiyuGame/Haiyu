using Haiyu.Models.Settings;
using Haiyu.Plugin.Common;
using Haiyu.Services.DialogServices;
using Haiyu.ViewModel.OOBEViewModels;
using Waves.Core.Helpers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Security.Credentials.UI;

namespace Haiyu.ViewModel;

public sealed partial class SettingViewModel : ViewModelBase
{
    public SettingViewModel(
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager,
        IKuroClient wavesClient,
        IKuroAccountService accountService,
        IAppContext<App> appContext,
        IViewFactorys viewFactorys,
        ITipShow tipShow,
        IScreenCaptureService screenCaptureService,
        IPickersService pickersService,
        IThemeService themeService,
        GithubIpSettings githubIpSettings,
        LanguageSelectViewModel languageSelectViewModel,
        RpcSettings rpcSettings
    )
    {
        DialogManager = dialogManager;
        WavesClient = wavesClient;
        AccountService = accountService;
        AppContext = appContext;
        ViewFactorys = viewFactorys;
        TipShow = tipShow;
        ScreenCaptureService = screenCaptureService;
        PickersService = pickersService;
        ThemeService = themeService;
        GithubIpSettings = githubIpSettings;
        LanguageSelectViewModel = languageSelectViewModel;
        RpcSettings = rpcSettings;
        RegisterMessanger();
    }

    private void RegisterMessanger()
    {
        this.Messenger.Register<CopyStringMessager>(this, CopyString);
        this.Messenger.Register<SkipGameVerifyWrapper>(this, SkipGameVerifyFileMethod);
    }

    private void CopyString(object recipient, CopyStringMessager message)
    {
        var package = new DataPackage();
        package.SetText(message.Value);
        Clipboard.SetContent(package);
    }

    public IDialogManager DialogManager { get; }
    public IKuroClient WavesClient { get; }
    public IKuroAccountService AccountService { get; }
    public IAppContext<App> AppContext { get; }
    public IViewFactorys ViewFactorys { get; }
    public ITipShow TipShow { get; }
    public IScreenCaptureService ScreenCaptureService { get; }
    public IPickersService PickersService { get; }
    public IThemeService ThemeService { get; }
    public GithubIpSettings GithubIpSettings { get; }
    public RpcSettings RpcSettings { get; }

    [ObservableProperty]
    public partial bool? StartGameAllowCloseMain { get; set; }

    [ObservableProperty]
    public partial int SelectCloseIndex { get; set; }

    [ObservableProperty]
    public partial bool ProgressAction { get; set; }

    [ObservableProperty]
    public partial bool CheckUpdateVisibility { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<LauncheBthWrapper> AppLauncheBths { get; set; } =
        LauncheBthWrapper.CreateDefault();

    [ObservableProperty]
    public partial LauncheBthWrapper SelectAppLauncheBth { get; set; }

    partial void OnSelectAppLauncheBthChanged(LauncheBthWrapper value)
    {
        if (value == null)
            return;
        _ = AppSettings.SetLauncheBthAsync(value.Memory);
    }

    [RelayCommand]
    async Task Loaded()
    {
        ProgressAction = true;
        var closeWindow = await AppSettings.GetCloseWindowAsync();
        switch (closeWindow)
        {
            case "True":
                this.SelectCloseIndex = 1;
                break;
            case "False":
                this.SelectCloseIndex = 0;
                break;
        }
        var wallpaperType = await AppSettings.GetWallpaperTypeAsync();
        if (wallpaperType == null)
        {
            this.SelectWallpaperName = WallpaperTypes[0];
        }
        else
        {
            if (wallpaperType == "Video")
            {
                this.SelectWallpaperName = WallpaperTypes[0];
            }
            else
            {
                this.SelectWallpaperName = WallpaperTypes[1];
            }
        }
        this.StartGameAllowCloseMain = await AppSettings.GetStartGameAllowCloseMainAsync();
        switch (await AppSettings.GetElementThemeAsync())
        {
            case "Light":
                this.SelectTheme = Themes[1];
                break;
            case "Dark":
                this.SelectTheme = Themes[2];
                break;
            case "Default":
                this.SelectTheme = Themes[0];
                break;
            default:
                this.SelectTheme = Themes[0];
                break;
        }
        await this.InitCapture();
        GetAllVersion();
        await LoadUpdateAppType();
        await LoadLauncheBth();
        await ReadVerifySkipFileAsync();
        ProgressAction = false;
    }

    private async Task ReadVerifySkipFileAsync()
    {
        this.SkipVerifyFiles = SkipGameVerifyWrapper.FromSettings(
            (await AppSettings.GetskipVerifyFilesAsync())
        );
        this.AutoSkipVerifyDelete = await AppSettings.GetverifySkilDeleteAsync(this.CTS.Token);
    }

    private async Task LoadLauncheBth()
    {
        var saveOption = await AppSettings.GetLauncheBthAsync();
        if (saveOption == null)
            return;
        foreach (var item in this.AppLauncheBths)
        {
            if (saveOption == item.Memory)
            {
                this.SelectAppLauncheBth = item;
                break;
            }
        }
    }

    [RelayCommand]
    async Task CopyToken()
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
        var account = AccountService.CurrentAccount;
        if (account is not null && await WavesClient.IsLoginAsync(account))
        {
            DataPackage package = new();
            package.SetText("NULL");
            Clipboard.SetContent(package);
        }
    }

    [RelayCommand]
    async Task CopyDid()
    {
        DataPackage package = new();
        package.SetText(HardwareIdGenerator.GenerateUniqueId());
        Clipboard.SetContent(package);
    }

    partial void OnSelectCloseIndexChanged(int value)
    {
        _ = OnSelectCloseIndexChangedAsync(value);
    }

    partial void OnStartGameAllowCloseMainChanged(bool? value)
    {
        if (value == null)
            return;
        _ = AppSettings.SetStartGameAllowCloseMainAsync(value);
    }

    private async Task OnSelectCloseIndexChangedAsync(int value)
    {
        switch (value)
        {
            case 0:
                await AppSettings.SetCloseWindowAsync("False");
                break;
            case 1:
                await AppSettings.SetCloseWindowAsync("True");
                break;
        }
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}
