using Haiyu.Services.DialogServices;
using System.Text;
using Waves.Api.Models.CloudGame;
using Waves.Core.Common;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.Models.CloudGame;
using Waves.Core.Models.Enums;
using Waves.Core.Services.CloudGameServices;
using Windows.Wdk;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Haiyu.ViewModel.GameViewModels;

public sealed partial class WavesCloudGameViewModel : ViewModelBase
{
    public IKuroCloudGameContext KuroCloudGameContext { get; }
    public IDialogManager DialogManager { get; }
    public IAppContext<App> App { get; }
    public ITipShow TipShow { get; }
    public IViewFactorys ViewFactorys { get; }
    public IWavesCloudGameService WavesCloudGameService { get; }
    public IWallpaperService WallpaperService { get; }

    [ObservableProperty]
    public partial ObservableCollection<CloudGameLoginSession> Logins { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial WallDataWrapper WallData { get; set; } = CreateEmptyWallData();

    [ObservableProperty]
    public partial int NodesCount { get; set; }


    CloudGameUIActive _startBthActive;

    // 用于阻止删除用户之前启动的余额请求在完成后把旧数据重新写回界面。
    private int _wallDataRefreshVersion;

    private static WallDataWrapper CreateEmptyWallData() => new()
    {
        FreeString = string.Empty,
        PlayerCardString = string.Empty,
        ExperienseTimeString = string.Empty,
        PayString = string.Empty
    };

    public WavesCloudGameViewModel(
        IWallpaperService wallpaperService,
        [FromKeyedServices(nameof(Waves.Core.Services.KuroCloudGameContext))]
            IKuroCloudGameContext kuroCloudGameContext,
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager,
        IAppContext<App> app,
        ITipShow tipShow,IViewFactorys viewFactorys,IWavesCloudGameService wavesCloudGameService
    )
    {
        WallpaperService = wallpaperService;
        KuroCloudGameContext = kuroCloudGameContext;
        DialogManager = dialogManager;
        App = app;
        TipShow = tipShow;
        ViewFactorys = viewFactorys;
        WavesCloudGameService = wavesCloudGameService;
        KuroCloudGameContext.CloudGameProcessTracker.OnProgressChanged +=
            CloudGameProcessTracker_OnProgressChanged;
        ;
        RegisterMessager();
    }



    private async void CloudGameProcessTracker_OnProgressChanged(CloudGameProcessTracker obj)
    {
        
        await App.TryInvokeAsync(async () =>
        {
            var state = await this.KuroCloudGameContext.GetCloudStateAsync();
            if (obj == null)
                return;
            if (obj.CoreType == CloudCoreType.OpeningWeb && obj.QueueResult != null)
            {
                BottomText = LanguageService.FormatByText(LanguageService.GetStringByText("正在游戏"));
                StartGameText = LanguageService.GetStringByText("停止游戏");
                if ((state.WindowHandle!=null && state.WindowHandle!= nint.MinValue) || !string.IsNullOrWhiteSpace(state.WindowTitleKey))
                {
                    //防止重复启动
                    return;
                }
                CloudGameWindows window = new CloudGameWindows(obj.QueueResult);
                var title = Guid.NewGuid().ToString();
                window.Title = title;
                window.Activate();

                this.KuroCloudGameContext.SetGameingWindow((nint)window.GetWindowHandle(), title);
                this._startBthActive = CloudGameUIActive.StopGame;
            }
            else if (obj.CoreType == CloudCoreType.QueueUp)
            {
                BottomText = LanguageService.FormatByText(LanguageService.GetStringByText("排队：{0}，{1}秒内"), obj.QueueQty, obj.QueueWaitSecond);
                StartGameText = LanguageService.GetStringByText("停止排队");
                this._startBthActive = CloudGameUIActive.QueueUp;
            }
            else
            {
                if (!(state.WindowHandle != null && state.WindowHandle != nint.MinValue) || !string.IsNullOrWhiteSpace(state.WindowTitleKey))
                {
                    this._startBthActive = CloudGameUIActive.StartGame;
                    StartGameText = LanguageService.GetStringByText("进入游戏");
                    BottomText = LanguageService.GetStringByText("准备就绪");
                }
                else
                {
                    Span<char> buffer = new char[512];
                    var len = Windows.Win32.PInvoke.GetWindowText(new HWND((IntPtr)state.WindowHandle), buffer);
                    var text = new string(buffer[..len]) ?? "";
                    StartGameText = LanguageService.GetStringByText("终止游戏");
                    BottomText = LanguageService.GetStringByText("游戏中");
                    this._startBthActive = CloudGameUIActive.StopGame;
                }
            }
        });
    }

    void RefreshUIAsync()
    {
        CloudGameProcessTracker_OnProgressChanged(this.KuroCloudGameContext.CloudGameProcessTracker);
    }

    private void RegisterMessager()
    {
        this.Messenger.Register<CloudLoginMessager>(this, CloudLoginMethod);
        this.Messenger.Register<RefreshGamePageMessager>(this, RefreshGamePageMethod);
        this.Messenger.Register<DeleteCloudUserMessager>(this, DeleteCloudUserMethod);
    }

    private void DeleteCloudUserMethod(object recipient, DeleteCloudUserMessager message)
    {
        if (string.IsNullOrWhiteSpace(this._userId))
            return;
        if(message.id == this._userId)
        {
            Interlocked.Increment(ref this._wallDataRefreshVersion);
            this.WallData = CreateEmptyWallData();
            this.UserName = string.Empty;
            this._userId = string.Empty;
        }
    }

    private async void RefreshGamePageMethod(object recipient, RefreshGamePageMessager message)
    {
        await this.Loaded();
    }

    private async void CloudLoginMethod(object recipient, CloudLoginMessager message)
    {
        await Task.Delay(2000);
        await this.RefreshUserAsync();
    }

    public override void Dispose()
    {
        KuroCloudGameContext.CloudGameProcessTracker.OnProgressChanged -=
            CloudGameProcessTracker_OnProgressChanged;
        base.Dispose();
    }

    [RelayCommand]
    async Task Loaded()
    {
        try
        {
            IsRefreshing = true;
            WallpaperService.SetMediaForUrl(
                Waves.Core.Models.Enums.WallpaperShowType.Image,
                "https://aki-gm-resources-back.aki-game.com/pv/cg/login.webp"
            );
            await RefreshUserAsync();
            this.RefreshUIAsync();
            IsRefreshing = false;
        }
        catch (Exception ex)
        {
            IsRefreshing = false;
            this.Logger.WriteError(ex.Message + ex.StackTrace);
        }
    }

    [RelayCommand]
    async Task InvokeTask()
    {
        if (this._startBthActive == CloudGameUIActive.StartGame)
        {
            var selectLogin = await this.WavesCloudGameService.GetCurrentUserSession();
            if (selectLogin == null)
            {
                await TipShow.ShowMessageAsync(LanguageService.GetStringByText("请在左侧卡片登录一个账号"), Symbol.Clear);
                return;
            }
            var wallData =
            await this.WavesCloudGameService.GetWalletDataAsync(
                selectLogin,
                this.CTS.Token
            );

            if (wallData == null || wallData.Data == null)
            {
                await TipShow.ShowMessageAsync(wallData.Msg ?? LanguageService.GetStringByText("获取余额失败！"), Symbol.Clear);
                return;
            }
            var result = await DialogManager.ShowSelectGameNodeAsync(
                selectLogin.GetId()
            );

            if (result == null || result.SelectNode == null)
            {
                await TipShow.ShowMessageAsync(LanguageService.GetStringByText("请选择节点或节点失效"), Symbol.Clear);
                return;
            }
            var qualityOpt = await this.GetOptionsAsync();
            if (qualityOpt == null)
            {
                await TipShow.ShowMessageAsync(LanguageService.GetStringByText("构建清晰度失败，日志已记录"), Symbol.Clear);
                return;
            }
            _ = Task.Run(async () =>
                await this.KuroCloudGameContext.StartGameAsync(
                    selectLogin,
                    result.Nodes,
                    result.SelectNode,
                    qualityOpt,
                    this.GetDefaultPayType(wallData.Data)
                )
            );
        }
        else if (_startBthActive == CloudGameUIActive.QueueUp)
        {
            await this.KuroCloudGameContext.StopQueueAsync();
        }
        else if (_startBthActive == CloudGameUIActive.StopGame)
        {
            this.KuroCloudGameContext.CloudGameEventPublisher.Publish(new(CloudCoreType.ReqExit));
        }
    }

    public uint GetDefaultPayType(WalletData walletInfo)
    {
        var timeCardinfo =
            DateTimeOffset.FromUnixTimeSeconds(walletInfo.TimeCardInfo.ExpireTimeSeconds)
            - DateTime.Now;
        if (walletInfo.TimeCardInfo is not null && (timeCardinfo.TotalSeconds > 0))
        {
            return (uint)CloudPayType.Pay;
        }
        if (
            walletInfo.ExperienceCardInfo is not null
            && (
                walletInfo.ExperienceCardInfo.Day > 0
                || walletInfo.ExperienceCardInfo.Hour > 0
                || walletInfo.ExperienceCardInfo.Minute > 0
                || walletInfo.ExperienceCardInfo.Second > 0
            )
        )
        {
            return (uint)CloudPayType.Experience; // 体验卡 → Experience(4)
        }
        var freeSeconds = walletInfo.FreeTimeInfo?.LeftSeconds ?? 0;
        var paySeconds = walletInfo.PayTimeInfo?.LeftSeconds ?? 0;
        if (freeSeconds > 0)
            return (uint)CloudPayType.Free;
        if (paySeconds > 0)
            return (uint)CloudPayType.Pay;
        return (uint)CloudPayType.Pay;
    }

    /// <summary>
    /// 构建当前设备最佳的清晰度
    /// </summary>
    /// <returns></returns>
    public async Task<StreamQualityOptions?> GetOptionsAsync()
    {
        try
        {
            var dpi = (int)HwndExtensions.GetDpiForWindow(App.App.MainWindow.GetWindowHandle());
            var area = DisplayArea.Primary.OuterBounds;
            return await KuroCloudGameContext.GetOptionsAsync(dpi, area.Width, area.Height);
        }
        catch (Exception ex)
        {
            Logger.WriteError($"构建清晰度出错:{ex.Message}");
            return null;
        }
    }

    [RelayCommand]
    async Task OpenSettingsDialog()
    {
        await DialogManager.ShowWavesCloudSettingAsync(GameType.Waves);
    }

    [RelayCommand]
    async Task ShowWavesAnalysis()
    {
        var selectLogin = await this.WavesCloudGameService.GetCurrentUserSession();
        if(selectLogin == null)
        {
            SystemEventMessager.Publish(new()
            {
                Message = LanguageService.GetStringByText("请登录并选中一个鸣潮账号")
            });
            return;
        }
        var win = ViewFactorys.ShowAnalysisRecordV2(selectLogin);
        var scale = Haiyu.Controls.TitleBar.GetScaleAdjustment(win);
        int targetDipWidth = 1200;
        int targetDipHeight = 750;
        win.Manager.Height = targetDipHeight;
        win.Manager.Width = targetDipWidth;
        win.AppWindow.Show();
    }

    [RelayCommand]
    async Task OpenWavesCloudManagerDialog()
    {
        await DialogManager.ShowCloudUserManagerDialogAsync();
        await RefreshCardAsync();
    }
}


public enum CloudGameUIActive:uint
{
    /// <summary>
    /// 终止游戏
    /// </summary>
    StopGame,
    /// <summary>
    /// 排队中
    /// </summary>
    QueueUp,
    /// <summary>
    /// 开始游戏
    /// </summary>
    StartGame
}
