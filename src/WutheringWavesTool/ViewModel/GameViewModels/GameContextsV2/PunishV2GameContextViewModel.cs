using Haiyu.ServiceHost.Contracts;
using Waves.Core.Common;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;
using Windows.ApplicationModel.DataTransfer;

namespace Haiyu.ViewModel.GameViewModels.GameContexts;

public partial class PunishV2GameContextViewModel : KuroGameContextViewModelV2
{
    public PunishV2GameContextViewModel(IAppContext<App> appContext, ITipShow tipShow)
        : base(appContext, tipShow)
    {
        WeakReferenceMessenger.Default.Register<LocalGameRefreshBindUser>(
            this,
            LocalGameRefreshBindUserMethod
        );
    }

    /// <summary>
    /// 是否正在刷新本地账户状态
    /// </summary>
    [ObservableProperty]
    public partial bool IsLocalUserRefresh { get; set; }

    [ObservableProperty]
    public partial PunishLocalGameRoleItem GameItem { get; set; }

    [ObservableProperty]
    public partial int SwatchIndex { get; set; }

    #region Cache
    [HaiyuCache(
        nameof(Starter),
        ExpirationSeconds = 3500,
        TargetName = nameof(WavesV2GameContextViewModel)
    )]
    public GameLauncherStarter Starter { get; set; }
    #endregion

    /// <summary>
    /// 本地账户标题信息
    /// </summary>
    [ObservableProperty]
    public partial string LocalUserTitle { get; set; }

    private async void LocalGameRefreshBindUserMethod(
        object recipient,
        LocalGameRefreshBindUser message
    )
    {
        if (message.data?.PlayerItem?.Type != GameType.Punish)
        {
            return;
        }
        await this.RefreshLocalGameUser(message.data);
    }

    [RelayCommand]
    private async Task RefreshLocalGameUser(KRSDKLauncherCacheWrapper wrapper = null)
    {
        await RefreshLocalGameUser(wrapper,true);
    }

    private async Task RefreshLocalGameUser(KRSDKLauncherCacheWrapper wrapper = null,
        bool isRefresh = false)
    {
        var cacheType = isRefresh ? HaiyuCacheMode.Refresh : HaiyuCacheMode.Default;
        IsLocalUserRefresh = true;
        var lastSelect = await this.GameContext.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LasterSelectLocalUser,
            this.CTS.Token
        );
        if (lastSelect == null)
        {
            LocalUserTitle = LanguageService.GetStringByText("请选择账号");
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
                LocalUserTitle = LanguageService.GetStringByText("请选择账号");
                IsLocalUserRefresh = false;
                return;
            }
            foreach (var item in localUsers)
            {
                var code = KrKeyHelper.Xor(item.OauthCode, 5);
                var userPlayerCache = await this.TryInvokeAsync(async () =>
                    await this.CacheService.GetOrCreateAsync(
                        nameof(PunishV2GameContextViewModel),
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
                    if (player is not PunishQueryPlayerItem punishPlayer)
                    {
                        continue;
                    }
                    KRSDKLauncherCacheWrapper info = new KRSDKLauncherCacheWrapper(
                        item,
                        punishPlayer
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
            IsLocalUserRefresh = false;
            return;
        }
        if (selectItem.PlayerItem is not PunishQueryPlayerItem playerItem)
        {
            IsLocalUserRefresh = false;
            return;
        }
        LocalUserTitle = playerItem.RoleName;
        var userPlayerInfoCache = await this.TryInvokeAsync(async () =>
            await this.CacheService.GetOrCreateAsync(
                nameof(PunishV2GameContextViewModel),
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
            IsLocalUserRefresh = false;
            return;
        }
        var result = userPlayerInfoCache.Result;
        var punishData = result.Items[0] as PunishLocalGameRoleItem;
        if (punishData != null)
        {
            LocalUserTitle = punishData.PlayerName;
            GameItem = punishData;
        }
        SwatchIndex = 1;
        IsLocalUserRefresh = false;
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

    [RelayCommand]
    public void CopyGameItemId()
    {
        if (GameItem == null)
            return;
        var package = new DataPackage();
        package.SetText(GameItem.RoleId.ToString());
        Clipboard.SetContent(package);
    }

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
    public partial string SelectTab { get; set; }

    partial void OnSelectTabChanged(string value)
    {
        if (value == null)
        {
            Contents.Clear();
            return;
        }
        if (value == Tabs[0])
        {
            if (Contents == null)
                return;
            Contents = Activity.Contents.ToObservableCollection();
        }
        else if (value == Tabs[1])
        {
            if (Notice == null)
                return;
            Contents = Notice.Contents.ToObservableCollection();
        }
        else if (value == Tabs[2])
        {
            if (Contents == null)
                return;
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

    public override GameType GameType => GameType.Punish;

    public override async Task ShowCardAsync(bool showCard)
    {
        if (showCard)
        {
            var starter = await this.TryCacheInvokeAsync(async ct =>
                await this.LoadStarterAsync(
                    $"{nameof(PunishV2GameContextViewModel)}:{this.GameContext.ContextName}",
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
            await RefreshLocalGameUser(null,false);
        }
        else
        {
            this.SelectTab = null;
            PlayerCardVisibility = Visibility.Collapsed;
        }
    }
}
