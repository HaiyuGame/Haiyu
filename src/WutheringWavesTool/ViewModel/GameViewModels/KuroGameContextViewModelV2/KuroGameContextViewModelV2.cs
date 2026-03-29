using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;
using Haiyu.Models.Dialogs;
using Haiyu.Services.DialogServices;
using Waves.Core.Common;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Haiyu.ViewModel.GameViewModels;

public abstract partial class KuroGameContextViewModelV2 : ViewModelBase
{
    public LoggerService Logger { get; }
    public IGameContextV2 GameContext { get; private set; }
    public IDialogManager DialogManager { get; }
    public IAppContext<App> AppContext { get; }
    public ITipShow TipShow { get; }
    public IWallpaperService WallpaperService { get; }

    protected KuroGameContextViewModelV2(IAppContext<App> appContext, ITipShow tipShow)
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
                : ServerDisplay.GetPunishV2Games;
        var openService =
            this.GameType == GameType.Waves
                ? AppSettings.WavesAutoOpenContext
                : AppSettings.PunishAutoOpenContext;

        var selectServer = Servers.Where(x => x.Key == openService).FirstOrDefault();
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
        }
        GC.Collect();
        await Task.Delay(500);
        this.CTS = new CancellationTokenSource();
        this.GameContext = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(name);
        GameContext.ProgressState.OnProgressChanged += ProgressState_OnProgressChanged;
        CurrentProgressValue = 0;
        await this.GameContext.ReEmitLastOutputAsync(true);
        var status = await this.GameContext.GetGameContextStatusAsync(this.CTS.Token);
        if (!status.PredownloaAcion)
        {
            await this.GameContext.ReEmitLastOutputAsync(false);
        }
        var dx11 = await GameContext.GameLocalConfig.GetConfigAsync(GameLocalSettingName.IsDx11);
        if (bool.TryParse(dx11, out var flag))
        {
            this.IsDx11Launcher = flag;
        }
        if (this.GameContext.GameType == GameType.Waves)
        {
            AppSettings.WavesAutoOpenContext = this.GameContext.ContextName;
        }
        else if (this.GameContext.GameType == GameType.Punish)
        {
            AppSettings.PunishAutoOpenContext = this.GameContext.ContextName;
        }
        await RefreshCoreAsync(showCard);
    }

    private async void ProgressState_OnProgressChanged(GameProgressTracker tracker)
    {
        var args = tracker.LastArgs;
        await AppContext.TryInvokeAsync(async () =>
        {
            if (this.GameContext == null)
                return;
            if (!tracker.Prod)
            {
                var activeFiles = tracker.ActiveFilesItem;
                if (ShouldReplaceActiveFilesItem(ActiveFilesItems, activeFiles))
                {
                    ActiveFilesItems = activeFiles;
                }
                this.MaxStep = tracker.AllSteps.Count;
                this.CurrentStep = tracker.CurrentStepIndex + 1;
                this.CurrentStepText = tracker.AllSteps[tracker.CurrentStepIndex];
                this.SpeedText = tracker.GetSpeedText();
                this.ActiveFile = System.IO.Path.GetFileName(tracker.FilePath);
                var status = await this.GameContext.GetGameContextStatusAsync(this.CTS.Token);
                var actionType = args.Type;
                if (
                    actionType == Waves.Core.Models.Enums.GameContextActionType.Download
                    || actionType == Waves.Core.Models.Enums.GameContextActionType.Verify
                    || actionType == Waves.Core.Models.Enums.GameContextActionType.Decompress
                )
                {
                    UpdateTransferProgressDisplay(tracker, args, status);
                    ShowGameDownloadingBth(status);
                }
                if (actionType == Waves.Core.Models.Enums.GameContextActionType.BottomText)
                {
                    ShowGameDownloadingBth(status);
                    this.MaxProgressValue = args.FileTotal;
                    this.CurrentProgressValue = args.CurrentFile;
                    this.BottomBarContent = args.DeleteString;
                    PauseStartEnable = false;
                }

                if (
                    actionType == Waves.Core.Models.Enums.GameContextActionType.None
                    || actionType == Waves.Core.Models.Enums.GameContextActionType.GameExit
                    || tracker.IsCancel
                )
                {
                    PauseStartEnable = true;
                    this.CurrentProgressValue = 0;
                    this.MaxProgressValue = 100;
                    if (!status.IsGameExists && !status.IsGameInstalled)
                    {
                        ShowSelectInstallBth(status);
                    }
                    if (status.IsGameExists && !status.IsGameInstalled)
                    {
                        ShowGameDownloadBth(status);
                    }
                    if (status.IsLauncher)
                    {
                        await ShowGameLauncherBth(
                            status.IsUpdate,
                            status.DisplayVersion,
                            status.Gameing
                        );
                    }
                    if (
                        status.IsGameExists
                        && !status.IsGameInstalled
                        && (status.IsPause || status.IsAction)
                    )
                    {
                        ShowGameDownloadingBth(status);
                        if (status.IsPause)
                        {
                            this.PauseIcon = "\uE768";
                        }
                        else
                        {
                            this.PauseIcon = "\uE769";
                        }
                    }
                    if (actionType == Waves.Core.Models.Enums.GameContextActionType.GameExit)
                    {
                        this.AppContext.App.MainWindow.Show();
                    }
                }
                if (
                    actionType == Waves.Core.Models.Enums.GameContextActionType.TipMessage
                    && !status.IsPredownloaded
                )
                {
                    await DialogManager.ShowMessageDialog(args.TipMessage, "确认", "关闭");
                }
                if (
                    actionType == Waves.Core.Models.Enums.GameContextActionType.CdnSelect
                    && !status.IsPredownloaded
                )
                {
                    ShowGameDownloadingBth(status);
                    PauseStartEnable = false;
                    BottomBarContent = args.TipMessage;
                }
            }
        });
    }

    private void UpdateTransferProgressDisplay(
        GameProgressTracker tracker,
        Waves.Core.Models.GameContextOutputArgs args,
        GameContextStatus status
    )
    {
        this.MaxProgressValue = tracker.TotalBytes;
        this.CurrentProgressValue = tracker.CurrentBytes;
        this.ProgressValue = tracker.Percentage;
        this.CurrentByteText = GameProgressTracker.FormatBytes(tracker.CurrentBytes);
        this.MaxByteText = GameProgressTracker.FormatBytes(tracker.TotalBytes);

        var isPaused = status.IsPause || tracker.IsPaused || (args.IsAction && args.IsPause);

        if (args.Type == Waves.Core.Models.Enums.GameContextActionType.Verify)
        {
            if (isPaused)
            {
                this.PauseIcon = "\uE768";
                this.BottomBarContent = "下载已经暂停";
            }
            else
            {
                this.PauseIcon = "\uE769";
                this.BottomBarContent = BuildTrackerProgressSummary(tracker);
            }
            PauseStartEnable = true;
            return;
        }

        if (args.Type == Waves.Core.Models.Enums.GameContextActionType.Download)
        {
            if (isPaused)
            {
                this.PauseIcon = "\uE768";
                this.BottomBarContent = "下载已经暂停";
            }
            else
            {
                this.PauseIcon = "\uE769";
                this.BottomBarContent = BuildTrackerProgressSummary(tracker);
            }
            PauseStartEnable = true;
            return;
        }

        if (args.Type == Waves.Core.Models.Enums.GameContextActionType.Decompress)
        {
            this.PauseIcon = "\uE769";
            this.BottomBarContent =
                $"[{args.CurrentDecompressCount}/{args.MaxDecompressValue}] 已解压:{Math.Round((double)args.CurrentSize / 1024 / 1024 / 1024, 2)}GB,剩余:{Math.Round((double)(args.TotalSize - args.CurrentSize) / 1024 / 1024 / 1024, 2)}GB";
            PauseStartEnable = false;
        }
    }

    private static string BuildTrackerProgressSummary(GameProgressTracker tracker)
    {
        var segments = new List<string>(6)
        {
            $"{tracker.Percentage:F2}%",
            $"Step {tracker.CurrentStepIndex + 1}/{Math.Max(1, tracker.TotalSteps)}: {tracker.StepName}",
        };

        if (!string.IsNullOrWhiteSpace(tracker.CurrentStepTip))
        {
            segments.Add(tracker.CurrentStepTip);
        }

        var speedText = tracker.GetSpeedText();
        if (!string.IsNullOrWhiteSpace(speedText))
        {
            segments.Add($"Speed: {speedText}");
        }

        segments.Add(
            $"Bytes: {GameProgressTracker.FormatBytes(tracker.CurrentBytes)} / {GameProgressTracker.FormatBytes(tracker.TotalBytes)}"
        );

        return string.Join(" | ", segments);
    }

    /// <summary>
    /// 按钮类型，用枚举替代魔法数字，便于扩展和可读性
    /// </summary>
    private enum ButtonActionType
    {
        None = 0,
        SelectInstall = 1,
        Downloading = 2,
        StartGame = 3,
        PrepareUpdate = 4,
        InGame = 5,
        InstallPreDownload = 6,
    }

    private ButtonActionType _buttonAction = ButtonActionType.None;
    private bool disposedValue;

    async Task RefreshCoreAsync(bool showCard = true)
    {
        try
        {
            ProcessAction = true;

            var status = await this.GameContext.GetGameContextStatusAsync(this.CTS.Token);
            if (!status.IsGameExists)
            {
                Logger.WriteInfo("未找到游戏文件，显示下载按钮");
                ShowSelectInstallBth(status);
            }
            if (status.IsGameExists && !status.IsLauncher)
            {
                Logger.WriteInfo("游戏文件存在，但不能启动，显示继续按钮");
                ShowGameDownloadBth(status);
            }
            else if (!status.IsAction && status.IsGameExists)
            {
                await ShowGameLauncherBth(status.IsUpdate, status.DisplayVersion, status.Gameing);
            }
            if ((status.IsPause || status.IsAction) && !status.PredownloaAcion)
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
                ShowGameDownloadingBth(status);
            }
            if (status.IsGameExists && !status.IsPause && status.IsAction)
            {
                this.PauseIcon = "\uE769";
            }
            if (status.IsGameExists && status.IsPause && status.IsAction)
            {
                this.PauseIcon = "\uE768";
            }
            var index = await this.GameContext.GetDefaultLauncherValue(this.CTS.Token);
            var background = await this.GameContext.GetLauncherBackgroundDataAsync(
                index.FunctionCode.Background,
                this.CTS.Token
            );
            var wallpaperType = AppSettings.WallpaperType;
            if (status.IsPredownloaded)
            {
                PredCardVisibility = Visibility.Visible;
                if (status.PredownloaAcion && !status.PredownloadedDone && status.IsPause) // 正在预下载但已经暂停
                {
                    PredCardVisibility = Visibility.Visible;
                    PredDownloadBthVisibility = Visibility.Collapsed;
                    PredDownloadingVisibility = Visibility.Visible;
                    PredDownloadDoneVisibility = Visibility.Collapsed;
                    PreDownloadIcon = "\uE769";
                }
                else if (status.PredownloaAcion && !status.PredownloadedDone && !status.IsPause)
                {
                    PredCardVisibility = Visibility.Visible;
                    PredDownloadBthVisibility = Visibility.Collapsed;
                    PredDownloadingVisibility = Visibility.Visible;
                    PredDownloadDoneVisibility = Visibility.Collapsed;
                    PreDownloadIcon = "\uE768";
                }
                else if (!status.PredownloadedDone)
                {
                    PredCardVisibility = Visibility.Visible;
                    PredDownloadBthVisibility = Visibility.Visible;
                    PredDownloadingVisibility = Visibility.Collapsed;
                    PredDownloadDoneVisibility = Visibility.Collapsed;
                }
                else
                {
                    PredCardVisibility = Visibility.Visible;
                    PredDownloadBthVisibility = Visibility.Collapsed;
                    PredDownloadDoneVisibility = Visibility.Visible;
                    this.PredDownloadingVisibility = Visibility.Collapsed;
                }
            }
            else
            {
                PredCardVisibility = Visibility.Collapsed;
            }
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
            this.DownloadSpeedValue = coreConfig.LimitSpeed / 1000 / 1000;
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
            var launcher = await GameContext.GetGameLauncherSourceAsync(null, this.CTS.Token);
            this.CurrentProgressValue = 0;
            this.MaxProgressValue = 0;
            var localPredVersion = await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.ProdDownloadVersion
            );
            var localVersion = await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.LocalGameVersion
            );
            var doneDownload = await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.ProdDownloadFolderDone
            );
            if (launcher == null)
                return;
            if (
                launcher.ResourceDefault.Version == localPredVersion
                && localVersion != launcher.ResourceDefault.Version
            )
            {
                if (bool.TryParse(doneDownload, out var done))
                {
                    _buttonAction = ButtonActionType.InstallPreDownload;
                    BottomBarContent = "游戏有更新";
                    LauncheContent = "安装更新";
                    DisplayVersion = localPredVersion;
                    EnableStartGameBth = true;
                    LauncherIcon = "\uE896";
                }
            }
            else
            {
                _buttonAction = ButtonActionType.PrepareUpdate;
                BottomBarContent = "游戏有更新";
                LauncheContent = "更新游戏";
                DisplayVersion = version;
                EnableStartGameBth = true;
                LauncherIcon = "\uE898";
            }
        }
        else
        {
            if (gameing)
            {
                _buttonAction = ButtonActionType.InGame;
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
                _buttonAction = ButtonActionType.StartGame;
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
        if (_buttonAction == ButtonActionType.SelectInstall)
        {
            //var result = await DialogManager.ShowSelectDownloadFolderAsync(
            //    this.GameContext.ContextType
            //);
            //if (result.Result == ContentDialogResult.None)
            //{
            //    return;
            //}
            //Logger.WriteInfo($"选择游戏安装路径：{result.InstallFolder},即将进入通知核心进行下载");
            //StartBackground(() => this.GameContext.StartDownloadTaskAsync(result.InstallFolder));
            StartBackground(() => this.GameContext.StartDownloadTaskAsync("D:\\Punish"));
        }
        else
        {
            Logger.WriteInfo($"继续更新触发");
            var launcher = await GameContext.GetGameLauncherSourceAsync(null, this.CTS.Token);
            var folder = await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.GameLauncherBassFolder
            );
            StartBackground(() => this.GameContext.StartDownloadTaskAsync(folder));
        }
    }

    [RelayCommand]
    async Task ShowSelectGameFolder()
    {
        if (_buttonAction == ButtonActionType.SelectInstall)
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
                this.PauseIcon = "\uE769";
                StartBackground(() =>
                    this.GameContext.StartDownloadTaskAsync(result.InstallFolder)
                );
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
            this.PauseIcon = "\uE769";
            var folder =
                await GameContext.GameLocalConfig.GetConfigAsync(
                    GameLocalSettingName.GameLauncherBassFolder
                ) ?? "";
            StartBackground(() => this.GameContext.StartDownloadTaskAsync(folder));
        }
    }

    /// <summary>
    /// 显示
    /// </summary>
    private void ShowSelectInstallBth(GameContextStatus status)
    {
        _buttonAction = ButtonActionType.SelectInstall;
        GameInputFolderBthVisibility = Visibility.Visible;
        GameInstallBthVisibility = Visibility.Visible;
        GameDownloadingBthVisibility = Visibility.Collapsed;
        GameLauncherBthVisibility = Visibility.Collapsed;
        if (status.LasterArgs == null)
            BottomBarContent = "游戏文件不存在";
    }

    private void ShowGameDownloadingBth(GameContextStatus status)
    {
        Logger.WriteInfo($"游戏正在下载中");
        _buttonAction = ButtonActionType.Downloading;
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
    private void ShowGameDownloadBth(GameContextStatus status)
    {
        _buttonAction = ButtonActionType.Downloading;
        GameInputFolderBthVisibility = Visibility.Collapsed;
        GameInstallBthVisibility = Visibility.Visible;
        GameDownloadingBthVisibility = Visibility.Collapsed;
        GameLauncherBthVisibility = Visibility.Collapsed;
        if (status.LasterArgs == null)
            BottomBarContent = "请点击右下角继续更新游戏";
    }

    [RelayCommand]
    async Task RepirGame()
    {
        var state = await this.GameContext.GetGameContextStatusAsync(this.CTS.Token);
        if (state.IsPredownloaded && state.PredownloaAcion)
        {
            await TipShow.ShowMessageAsync("预下载期间禁止修复游戏！", Symbol.Clear);
            return;
        }
        if (
            (
                await DialogManager.ShowMessageDialog(
                    "修复游戏会将游戏缓存全部删除，保持与服务器最新文件保持一致\r\n（包含画面设置、滤镜设置、预下载等内容)",
                    "确认修复",
                    "取消"
                )
            ) == ContentDialogResult.Primary
        )
        {
            Logger.WriteInfo($"开始尝试修复游戏文件");
            await GameContext.RepairGameAsync();
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
        var state = await this.GameContext.GetGameContextStatusAsync(this.CTS.Token);
        if (state.IsPredownloaded && state.PredownloaAcion)
        {
            await TipShow.ShowMessageAsync("预下载期间禁止修复游戏！", Symbol.Clear);
            return;
        }
        Logger.WriteInfo($"删除游戏文件");
        await GameContext.DeleteResourceAsync();
        this.GameContext.GameEventPublisher.Publish(
            new GameContextOutputArgs() { Type = GameContextActionType.None }
        );
        //await this.GameContext_GameContextOutput(
        //    this,
        //    new GameContextOutputArgs()
        //    {
        //        Type = Waves.Core.Models.Enums.GameContextActionType.None,
        //    }
        //);
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
                DisposeAfter();
            }
            disposedValue = true;
        }
    }

    private void StartBackground(Func<Task> taskFunc)
    {
        Task.Run(async () =>
        {
            try
            {
                await taskFunc();
            }
            catch (OperationCanceledException)
            {
                Logger.WriteInfo("后台任务已取消");
            }
            catch (Exception ex)
            {
                Logger.WriteError($"后台任务异常: {ex.Message}");
                await AppContext.TryInvokeAsync(() =>
                    TipShow.ShowMessageAsync(ex.Message, Symbol.Clear)
                );
            }
        });
    }
}
