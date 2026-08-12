using Haiyu.ServiceHost.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Waves.Core.Common;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;
using Windows.ApplicationModel.DataTransfer;
using ZXing.QrCode.Internal;

namespace Haiyu.ViewModel.GameViewModels.GameContexts;

public partial class WavesV2GameContextViewModel : KuroGameContextViewModelV2
{
    public WavesV2GameContextViewModel(IAppContext<App> appContext, ITipShow tipShow)
        : base(appContext, tipShow)
    {
        WeakReferenceMessenger.Default.Register<LocalGameRefreshBindUser>(
            this,
            LocalGameRefreshBindUserMethod
        );
    }

    private async void LocalGameRefreshBindUserMethod(
        object recipient,
        LocalGameRefreshBindUser message
    )
    {
        if (message.data?.PlayerItem?.Type != GameType.Waves)
        {
            return;
        }
        await this.RefreshLocalGameUser(message.data);
    }

    public override void DisposeAfter()
    {
        if (this.Contents != null)
            this.Contents.Clear();

        this.Activity = null;
        this.Notice = null;
        this.News = null;

        if (this.SlideShows != null)
        {
            this.SlideShows.Clear();
            this.SlideShows = null;
        }
    }

    public override Task LoadAfter()
    {
        return Task.CompletedTask;
    }

    public override GameType GameType => GameType.Waves;

    [ObservableProperty]
    public partial ObservableCollection<Slideshow> SlideShows { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<string> Tabs { get; set; } =
        new ObservableCollection<string>()
        {
            LanguageService.GetStringByText("活动"),
            LanguageService.GetStringByText("公告"),
            LanguageService.GetStringByText("新闻"),
        };

    [ObservableProperty]
    public partial string? SelectTab { get; set; }

    [ObservableProperty]
    public partial int SwatchIndex { get; set; }

    [ObservableProperty]
    public partial KRSDKLauncherCacheWrapper SelectDisplayerLocalUser { get; set; }

    #region Cache
    [HaiyuCache(
        nameof(Starter),
        ExpirationSeconds = 3500,
        TargetName = nameof(WavesV2GameContextViewModel)
    )]
    public GameLauncherStarter Starter { get; set; }
    #endregion

    partial void OnSelectTabChanged(string value)
    {
        if (value == null)
        {
            Contents.Clear();
            return;
        }
        if (value == Tabs[0])
        {
            Contents = Activity.Contents.ToObservableCollection();
        }
        else if (value == Tabs[1])
        {
            Contents = Notice.Contents.ToObservableCollection();
        }
        else if (value == Tabs[2])
        {
            Contents = News.Contents.ToObservableCollection();
        }
    }

    #region Datas
    public Notice Notice { get; private set; }
    public News News { get; private set; }
    public Waves.Api.Models.Activity Activity { get; private set; }

    [ObservableProperty]
    public partial Visibility PlayerCardVisibility { get; set; }

    #endregion

    [ObservableProperty]
    public partial ObservableCollection<Content> Contents { get; set; } = new();

    #region 本地账户卡片

    /// <summary>
    /// 是否正在刷新本地账户状态
    /// </summary>
    [ObservableProperty]
    public partial bool IsLocalUserRefresh { get; set; }

    /// <summary>
    /// 本地账户标题信息
    /// </summary>
    [ObservableProperty]
    public partial string LocalUserTitle { get; set; }

    [ObservableProperty]
    public partial Base Base { get; private set; }

    [ObservableProperty]
    public partial MusicData MusicData { get; private set; }

    [ObservableProperty]
    public partial BattlePass BattlePass { get; private set; }

    [ObservableProperty]
    public partial MotorData MotorData { get; private set; }

    #endregion

    public async override Task ShowCardAsync(bool showCard)
    {
        if (showCard)
        {
            var starter = await this.TryCacheInvokeAsync(async ct =>
                await this.LoadStarterAsync(
                    $"{nameof(WavesV2GameContextViewModel)}:{this.GameContext.ContextName}",
                    async _ =>
                    {
                        var starter = await this.GameContext.GetLauncherStarterAsync(
                            this.CTS.Token
                        );
                        return starter;
                    },
                    HaiyuCacheMode.Default,
                    ct
                )
            );
            if (starter.Code == 0 && starter.Result != null)
            {
                this.SlideShows = starter.Result.Slideshow.ToObservableCollection();
                this.Notice = starter.Result.Guidance.Notice;
                this.News = starter.Result.Guidance.News;
                this.Activity = starter.Result.Guidance.Activity;
                PlayerCardVisibility = Visibility.Visible;
                this.SelectTab = null;
                this.SelectTab = Tabs[0];
            }
            await RefreshLocalGameUser(null, false);
        }
        else
        {
            this.Base = null;
            this.MusicData = null;
            this.BattlePass = null;
            this.MotorData = null;
            this.SelectTab = null;
            PlayerCardVisibility = Visibility.Collapsed;
        }
    }

    [RelayCommand]
    private async Task RefreshLocalGameUser(KRSDKLauncherCacheWrapper wrapper = null)
    {
        await RefreshLocalGameUser(wrapper, true);
    }

    private async Task RefreshLocalGameUser(
        KRSDKLauncherCacheWrapper wrapper = null,
        bool isRefresh = false
    )
    {
        var cacheType = isRefresh ? HaiyuCacheMode.Refresh : HaiyuCacheMode.Default;
        IsLocalUserRefresh = true;
        var lastSelect = await this.GameContext.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LasterSelectLocalUser,
            this.CTS.Token
        );
        if (lastSelect == null)
        {
            LocalUserTitle = LanguageService.GetStringByText("请在右侧选择本地游戏账号信息");
            IsLocalUserRefresh = false;
            SwatchIndex = 2;
            return;
        }
        KRSDKLauncherCacheWrapper? selectItem = null;
        if (wrapper != null)
        {
            selectItem = wrapper;
        }
        else
        {
            var localUsers = await this.GameContext.GetLocalGameOAuthAsync(this.CTS.Token);
            if (localUsers == null || localUsers.Count == 0)
            {
                LocalUserTitle = LanguageService.GetStringByText("未获取到本地游戏账号信息");
                this.Base = null;
                this.MusicData = null;
                this.BattlePass = null;
                this.MotorData = null;
                return;
            }
            foreach (var item in localUsers)
            {
                var code = KrKeyHelper.Xor(item.OauthCode, 5);
                var userPlayerCache = await this.TryInvokeAsync(async () =>
                    await this.CacheService.GetOrCreateAsync(
                        nameof(WavesV2GameContextViewModel),
                        nameof(QueryPlayerInfo),
                        $"{nameof(QueryPlayerInfo)}:{item.Id}",
                        TimeSpan.FromSeconds(400),
                        async ct => await GameContext.QueryPlayerInfoAsync(code, ct),
                        cacheType,
                        CTS.Token
                    )
                );
                if (userPlayerCache.Code != 0 || userPlayerCache.Result == null)
                {
                    continue;
                }
                var userPlayers = userPlayerCache.Result;
                if (userPlayers == null || userPlayers.Code != 0)
                {
                    continue;
                }
                foreach (var player in userPlayers.Items)
                {
                    if (player is not WavesQueryPlayerItem wavesPlayer)
                    {
                        continue;
                    }
                    KRSDKLauncherCacheWrapper info = new KRSDKLauncherCacheWrapper(
                        item,
                        wavesPlayer
                    );
                    if (info.GetKey == lastSelect)
                    {
                        selectItem = info;
                        break;
                    }
                }
            }
        }
        if (selectItem == null)
        {
            LocalUserTitle = LanguageService.GetStringByText("请选择账号");
            this.Base = null;
            this.MusicData = null;
            this.BattlePass = null;
            this.MotorData = null;
            IsLocalUserRefresh = false;
            return;
        }
        if (selectItem.PlayerItem is not WavesQueryPlayerItem playerItem)
        {
            IsLocalUserRefresh = false;
            return;
        }
        LocalUserTitle = playerItem.RoleName;
        var userPlayerInfoCache = await this.TryInvokeAsync(async () =>
            await this.CacheService.GetOrCreateAsync(
                nameof(WavesV2GameContextViewModel),
                nameof(QueryRoleInfo),
                $"{nameof(QueryRoleInfo)}:{playerItem.Id}",
                TimeSpan.FromSeconds(400),
                async ct =>
                    await this.GameContext.QueryRoleInfoAsync(
                        KrKeyHelper.Xor(selectItem.Cache.OauthCode, 5),
                        playerItem.Id,
                        playerItem.ServerName
                    ),
                HaiyuCacheMode.Default,
                CTS.Token
            )
        );
        if (userPlayerInfoCache.Code != 0 || userPlayerInfoCache.Result == null)
        {
            LocalUserTitle = LanguageService.GetStringByText("获取账号信息失败");
            await TipShow.ShowMessageAsync(
                LanguageService.GetStringByText("请重新进入游戏获取信息"),
                Symbol.Clear
            );
            this.GameContext.SystemEventPublisher.Publish(
                new()
                {
                    Message = LanguageService.FormatByText(
                        LanguageService.GetStringByText("体力卡片刷新失败，请重新进入游戏获取")
                    ),
                    Delay = 5,
                }
            );
            IsLocalUserRefresh = false;
            this.Base = null;
            this.MusicData = null;
            this.BattlePass = null;
            this.MotorData = null;
            return;
        }
        var result = userPlayerInfoCache.Result;
        var wavesData = (result.Items[0] as WavesLocalGameRoleItem)!;
        this.Base = wavesData.Base ?? new();
        this.MusicData = wavesData.MusicData;
        this.BattlePass = wavesData.BattlePass ?? new();
        this.MotorData = wavesData.MotorData ?? new();
        SwatchIndex = 1;
        IsLocalUserRefresh = false;
    }

    [RelayCommand]
    public void CopyGameItemId()
    {
        if (Base.Id == null)
            return;
        var package = new DataPackage();
        package.SetText(Base.Id.ToString());
        Clipboard.SetContent(package);
    }
}
