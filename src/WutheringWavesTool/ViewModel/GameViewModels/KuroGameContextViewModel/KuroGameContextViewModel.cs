using System.Diagnostics.Contracts;
using Haiyu.Models.Dialogs;
using Haiyu.Services.DialogServices;
using Haiyu.ViewModel.GameViewModels.Contracts;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Haiyu.ViewModel.GameViewModels;

public abstract partial class KuroGameContextViewModel
    : ViewModelBase,
        IKuroGameContextViewModelBase
{
    public LoggerService Logger { get; }
    public IGameContext GameContext { get; private set; }
    public IDialogManager DialogManager { get; }
    public IAppContext<App> AppContext { get; }
    public ITipShow TipShow { get; }
    public IWallpaperService WallpaperService { get; }

    protected KuroGameContextViewModel(IAppContext<App> appContext, ITipShow tipShow)
    {
        this.Logger = Instance.Host.Services.GetKeyedService<LoggerService>("AppLog");
        DialogManager = Instance.Host.Services.GetRequiredKeyedService<IDialogManager>(
            nameof(MainDialogService)
        );
        AppContext = appContext;
        TipShow = tipShow;
        WallpaperService = Instance.GetService<IWallpaperService>();
        this.Servers =
            this.GameType == GameType.Waves
                ? ServerDisplay.GetWavesGames
                : ServerDisplay.GetPunishGames;
        var openService = this.GameType == GameType.Waves?AppSettings.WavesAutoOpenContext:AppSettings.PunishAutoOpenContext;

        var selectServer = Servers.Where(x=>x.Key == openService).FirstOrDefault();
        this.SelectServer = selectServer == null ? Servers[0] : selectServer;
    }

    public static List<string> GetWavesServers() =>
        [
            nameof(WavesBiliBiliGameContext),
            nameof(WavesGlobalGameContext),
            nameof(WavesMainGameContext),
        ];



    [ObservableProperty]
    public partial bool IsDx11Launcher { get; set; } = false;

    async partial void OnIsDx11LauncherChanged(bool value)
    {
        await this.GameContext.GameLocalConfig.SaveConfigAsync(
            GameLocalSettingName.IsDx11,
            value == true ? "true" : "false"
        );
    }

    #region 下载显示

    /// <summary>
    /// 选择下载路径显示
    /// </summary>
    [ObservableProperty]
    public partial Visibility GameInstallBthVisibility { get; set; } = Visibility.Collapsed;

    /// <summary>
    /// 定位游戏路径显示
    /// </summary>
    [ObservableProperty]
    public partial Visibility GameInputFolderBthVisibility { get; set; } = Visibility.Collapsed;

    /// <summary>
    /// 游戏下载中
    /// </summary>
    [ObservableProperty]
    public partial Visibility GameDownloadingBthVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility GameLauncherBthVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial string LauncherIcon { get; set; }

    [ObservableProperty]
    public partial ImageSource VersionLogo { get; set; }

    [ObservableProperty]
    public partial string LauncheContent { get; set; }

    [ObservableProperty]
    public partial string DisplayVersion { get; set; }
    #endregion

    [ObservableProperty]
    public partial string PauseIcon { get; set; }

    [ObservableProperty]
    public partial bool PauseStartEnable { get; set; } = true;

    [ObservableProperty]
    public partial string BottomBarContent { get; set; }

    [ObservableProperty]
    public partial bool EnableStartGameBth { get; set; } = false;

    [ObservableProperty]
    public partial ObservableCollection<ServerDisplay> Servers { get; set; }

    [ObservableProperty]
    public partial ServerDisplay SelectServer { get; set; }

    [ObservableProperty]
    public partial bool ProcessAction { get; set; } = false;
    public abstract GameType GameType { get; }

    async partial void OnSelectServerChanged(ServerDisplay value)
    {
        await SelectGameContextAsync(value.Key, value.ShowCard);
    }

    public async Task SelectGameContextAsync(string name, bool showCard)
    {
        if (this.GameContext != null)
        {
            await this.CTS?.CancelAsync();
            this.CTS = null;
            GameContext.GameContextOutput -= GameContext_GameContextOutput;
        }
        GC.Collect();
        this.CTS = new CancellationTokenSource();
        this.GameContext = Instance.Host.Services.GetRequiredKeyedService<IGameContext>(name);
        CurrentProgressValue = 0;
        GameContext.GameContextOutput += GameContext_GameContextOutput;
        var dx11 = await GameContext.GameLocalConfig.GetConfigAsync(GameLocalSettingName.IsDx11);
        if (bool.TryParse(dx11, out var flag))
        {
            this.IsDx11Launcher = flag;
        }
        if(this.GameContext.GameType == GameType.Waves)
        {
            AppSettings.WavesAutoOpenContext = this.GameContext.ContextName;
        }
        else if(this.GameContext.GameType == GameType.Punish)
        {
            AppSettings.PunishAutoOpenContext = this.GameContext.ContextName;
        }
        await RefreshCoreAsync(showCard);
    }

    /// <summary>
    /// 按钮类型,1为安装游戏,2为下载游戏,3为开始游戏,4为准备更新,5为游戏中
    /// </summary>
    private int _bthType = 0;
    private bool disposedValue;

    async Task RefreshCoreAsync(bool showCard = true)
    {
        try
        {
            ProcessAction = true;
            var status = await this.GameContext.GetGameContextStatusAsync(this.CTS.Token);
            if (!status.IsGameExists && !status.IsGameInstalled)
            {
                Logger.WriteInfo("未找到游戏文件，显示下载按钮");
                ShowSelectInstallBth();
            }
            if (status.IsGameExists && !status.IsGameInstalled && !status.IsLauncher)
            {
                Logger.WriteInfo("游戏文件存在，但不能启动，显示继续按钮");
                ShowGameDownloadBth();
            }
            else if (!status.IsAction && status.IsGameExists && status.IsGameInstalled)
            {
                await ShowGameLauncherBth(status.IsUpdate, status.DisplayVersion, status.Gameing);
            }
            if ((status.IsPause || status.IsAction))
            {
                if (status.IsAction && status.IsPause)
                {
                    this.BottomBarContent = "下载已经暂停";
                    this.PauseIcon = "\uE896";
                }
                else
                {
                    this.PauseIcon = "\uE769";
                }
                ShowGameDownloadingBth();
            }
            if (status.IsGameExists && status.IsGameInstalled && !status.IsPause && status.IsAction)
            {
                this.PauseIcon = "\uE769";
            }
            if (status.IsGameExists && status.IsGameInstalled && status.IsPause && status.IsAction)
            {
                this.PauseIcon = "\uE768";
            }
            var index = await this.GameContext.GetDefaultLauncherValue(this.CTS.Token);
            var background = await this.GameContext.GetLauncherBackgroundDataAsync(
                index.FunctionCode.Background,
                this.CTS.Token
            );
            var wallpaperType = AppSettings.WallpaperType;
            if (wallpaperType == "Video")
            {
                WallpaperService.SetMediaForUrl(
                    Waves.Core.Models.Enums.WallpaperShowType.Video,
                    background.BackgroundFile
                );
            }
            else
            {
                WallpaperService.SetMediaForUrl(
                    Waves.Core.Models.Enums.WallpaperShowType.Image,
                    background.FirstFrameImage
                );
            }
            this.VersionLogo = new BitmapImage(new(background.Slogan));
            var coreConfig = await GameContext.ReadContextConfigAsync(this.CTS.Token);
            this.DownloadSpeedValue = coreConfig.LimitSpeed/1000/1000;
            await ShowCardAsync(showCard);
            await LoadAfter();
            ProcessAction = false;
        }
        catch (Exception ex)
        {
            TipShow.ShowMessage(ex.Message, Symbol.Clear);
        }
    }

    public abstract Task ShowCardAsync(bool showCard);

    private async Task ShowGameLauncherBth(bool isUpdate, string version, bool gameing)
    {
        GameInputFolderBthVisibility = Visibility.Collapsed;
        GameInstallBthVisibility = Visibility.Collapsed;
        GameDownloadingBthVisibility = Visibility.Collapsed;
        GameLauncherBthVisibility = Visibility.Visible;
        if (isUpdate)
        {
            _bthType = 4;
            this.CurrentProgressValue = 0;
            this.MaxProgressValue = 0;
            Logger.WriteInfo("游戏版本有更新");
            BottomBarContent = "游戏有更新";
            LauncheContent = "更新游戏";
            DisplayVersion = version;
            EnableStartGameBth = true;
            LauncherIcon = "\uE898";
           
            
        }
        else
        {
            if (gameing)
            {
                _bthType = 5;
                this.CurrentProgressValue = 0;
                this.MaxProgressValue = 0;
                BottomBarContent = "游戏正在进行";
                LauncheContent = "正在运行";
                EnableStartGameBth = false;
                DisplayVersion = version;
                LauncherIcon = "\uE71A";
            }
            else
            {
                _bthType = 3;
                this.CurrentProgressValue = 0;
                this.MaxProgressValue = 0;
                var totalTime = await GameContext.GameLocalConfig.GetConfigAsync(
                    GameLocalSettingName.GameRunTotalTime
                );
                if (totalTime == null)
                {
                    BottomBarContent = "游戏准备就绪";
                }
                else
                {
                    if (int.TryParse(totalTime, out var timeResult))
                    {
                        var tt = TimeSpan.FromSeconds(timeResult);
                        BottomBarContent =
                            "已游玩" + ($"{tt.Days}天{tt.Hours}小时{tt.Minutes}分钟");
                        ;
                    }
                    else
                    {
                        BottomBarContent = "游戏准备就绪";
                    }
                }
                LauncheContent = "进入游戏";
                EnableStartGameBth = true;
                DisplayVersion = version;
                LauncherIcon = "\uE7FC";
            }
        }
    }

    [RelayCommand]
    async Task ShowSelectInstallFolder()
    {
        if (_bthType == 1)
        {
            var result = await DialogManager.ShowSelectDownloadFolderAsync(
                this.GameContext.ContextType
            );
            if (result.Result == ContentDialogResult.None)
            {
                return;
            }
            Logger.WriteInfo($"选择游戏安装路径：{result.InstallFolder},即将进入通知核心进行下载");
            Task.Factory.StartNew(async () =>
            {
                await this.GameContext.StartDownloadTaskAsync(
                    result.InstallFolder,
                    result.Launcher
                );
            });
        }
        else
        {
            Logger.WriteInfo($"继续更新触发");
            var launcher = await GameContext.GetGameLauncherSourceAsync(null, this.CTS.Token);
            Task.Factory.StartNew(async () =>
                await this.GameContext.StartDownloadTaskAsync(
                    await GameContext.GameLocalConfig.GetConfigAsync(
                        GameLocalSettingName.GameLauncherBassFolder
                    ),
                    launcher
                )
            );
        }
    }

    [RelayCommand]
    async Task ShowSelectGameFolder()
    {
        if (_bthType == 1)
        {
            var result = await DialogManager.ShowSelectGameFolderAsync(
                this.GameContext.ContextType
            );
            if (result.Result == ContentDialogResult.None)
            {
                return;
            }
            Logger.WriteInfo($"选择游戏安装文件：{result.InstallFolder}");
            if (File.Exists(result.InstallFolder + $"//{this.GameContext.Config.GameExeName}"))
            {
                Task.Factory.StartNew(async () =>
                {
                    await this.GameContext.StartDownloadTaskAsync(
                        result.InstallFolder,
                        result.Launcher
                    );
                });
            }
            else
            {
                TipShow.ShowMessage("选择文件路径不合法，请重新选择", Symbol.Clear);
            }
        }
        else
        {
            Logger.WriteInfo($"继续进行下载");
            var launcher = await GameContext.GetGameLauncherSourceAsync(null, this.CTS.Token);
            Task.Factory.StartNew(async () =>
            {
                await this.GameContext.StartDownloadTaskAsync(
                    await GameContext.GameLocalConfig.GetConfigAsync(
                        GameLocalSettingName.GameLauncherBassFolder
                    ) ?? "",
                    launcher
                );
            });
        }
    }

    /// <summary>
    /// 显示
    /// </summary>
    private void ShowSelectInstallBth()
    {
        _bthType = 1;
        GameInputFolderBthVisibility = Visibility.Visible;
        GameInstallBthVisibility = Visibility.Visible;
        GameDownloadingBthVisibility = Visibility.Collapsed;
        GameLauncherBthVisibility = Visibility.Collapsed;
        BottomBarContent = "游戏文件不存在";
    }

    private void ShowGameDownloadingBth()
    {
        Logger.WriteInfo($"游戏正在下载中");
        _bthType = 2;
        if (GameDownloadingBthVisibility == Visibility.Visible)
            return;
        GameInputFolderBthVisibility = Visibility.Collapsed;
        GameInstallBthVisibility = Visibility.Collapsed;
        GameLauncherBthVisibility = Visibility.Collapsed;
        GameDownloadingBthVisibility = Visibility.Visible;
    }

    /// <summary>
    /// 显示继续下载
    /// </summary>
    private void ShowGameDownloadBth()
    {
        _bthType = 2;
        GameInputFolderBthVisibility = Visibility.Collapsed;
        GameInstallBthVisibility = Visibility.Visible;
        GameDownloadingBthVisibility = Visibility.Collapsed;
        GameLauncherBthVisibility = Visibility.Collapsed;
        BottomBarContent = "请点击右下角继续更新游戏";
    }

    [RelayCommand]
    async Task RepirGame()
    {
        if (
            (
                await DialogManager.ShowMessageDialog(
                    "修复游戏会将游戏缓存全部删除，保持与服务器最新文件保持一致\r\n（包含画面设置、滤镜设置等内容)",
                    "确认修复",
                    "取消"
                )
            ) == ContentDialogResult.Primary
        )
        {
            Logger.WriteInfo($"开始尝试修复游戏文件");
            await GameContext.RepirGameAsync();
        }
        else
        {
            Logger.WriteInfo($"取消修复文件");
        }
    }

    [RelayCommand]
    async Task ShowGameResource()
    {
        await DialogManager.ShowGameResourceDialogAsync(this.GameContext.ContextName);
    }

    [RelayCommand]
    async Task DeleteGameResource()
    {
        Logger.WriteInfo($"删除游戏文件");
        await GameContext.DeleteResourceAsync();
        await this.GameContext_GameContextOutput(
            this,
            new GameContextOutputArgs()
            {
                Type = Waves.Core.Models.Enums.GameContextActionType.None,
            }
        );
    }

    [RelayCommand]
    async Task ShowGameLauncherCache()
    {
        var data = await this.GameContext.GetLocalGameOAuthAsync(this.CTS.Token);
        if (data == null)
        {
            TipShow.ShowMessage("不存在任何登陆信息，请登陆游戏后再次查看", Symbol.Clear);
            return;
        }

        await DialogManager.ShowGameLauncherChacheDialogAsync(
            new GameLauncherCacheArgs()
            {
                Datas = data,
                GameContextName = this.GameContext.ContextName,
            }
        );
    }

    public abstract Task LoadAfter();

    public abstract void DisposeAfter();

    public override void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                GameContext.GameContextOutput -= GameContext_GameContextOutput;
                DisposeAfter();
            }
            disposedValue = true;
        }
    }
}
