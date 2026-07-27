using System.Diagnostics.Contracts;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Haiyu.Helpers;
using Haiyu.Models.Wrapper.Wiki;
using Waves.Api.Models.GameWikiiClient;
using Waves.Core.Services;

namespace Haiyu.ViewModel.WikiViewModels;

public partial class WavesWikiViewModel : WikiViewModelBase
{
    private static readonly WindowsOption SignWindowOption =
        new()
        {
            Width = 400,
            Height = 400,
            MaxWidth = 400,
            MaxHeight = 400,
            IsResizable = false,
            IsMaximizable = false,
            CenterOnScreen = true,
        };

    private static readonly WindowsOption CommunityWindowOption =
        new()
        {
            Width = 400,
            Height = 700,
            IsResizable = true,
            CenterOnScreen = true,
        };

    private static readonly WindowsOption CommunityMapOption = new()
    {
        Width = 1000,
        Height = 500,
        IsResizable = false,
        CenterOnScreen = false
    };

    public IKuroClient KuroClient { get; }
    public IKuroAccountService KuroAccountService { get; }
    public WavesWikiViewModel(IAppContext<App> appContext,IKuroClient  kuroClient,IKuroAccountService kuroAccountService)
    {
        this.Messenger.Register<SelectUserMessanger>(this, LoginMessangerMethod);
        AppContext = appContext;
        this.KuroClient = kuroClient;
        this.KuroAccountService = kuroAccountService;
    }

    private async void LoginMessangerMethod(object recipient, SelectUserMessanger message)
    {
        await Loaded();
    }

    [ObservableProperty]
    public partial ObservableCollection<HotContentSideWrapper> Actives { get; set; } = [];

    [ObservableProperty]
    public partial bool Loading { get; set; }

    [ObservableProperty]
    public partial bool KuroLogin { get; set; } = false;


    [ObservableProperty]
    public partial ObservableCollection<EventContentSideWrapper>? RoleActive { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<EventContentSideWrapper>? WeaponActive { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<WikiCatalogueChildren> CatalogueChildren { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<GameRoilDataItem> Gamers { get; set; }

    [ObservableProperty]
    public partial GameRoilDataItem SelectGamer { get; set; }
    public IAppContext<App> AppContext { get; }

    [RelayCommand]
    async Task Loaded()
    {
        Loading = true;
        var wikiPage = await TryInvokeAsync(async () =>
            await this.GameWikiClient.GetHomePageAsync(WikiType.Waves, this.CTS.Token)
        );
        await RefreshUserAsync();
        if ((wikiPage.Result != null && wikiPage.Result.Data.ContentJson.Shortcuts != null))
        {
            Actives = GameWikiClient.GetEventData(wikiPage.Result)!.Format(WikiType.Waves)??[];
            var sides = wikiPage.Result.Data.ContentJson.SideModules.Where(x => x.Type == "events-side").ToList();
            if(sides.Count == 2)
            {
                var role =  await FormatSideDataAsync(sides[0]);
                RoleActive = role?.ToObservableCollection();
                var weapon =  await FormatSideDataAsync(sides[1]);
                WeaponActive = weapon?.ToObservableCollection();
            }
            else
            {
                TipShow.ShowMessage(LanguageService.GetStringByText("获取卡池信息出现了不可预料的情况，请确认官方Wiki显示是否正常"), Symbol.Clear);
            }

        }
        else
        {
            TipShow.ShowMessage(LanguageService.FormatByText(LanguageService.GetStringByText("获取数据失败，请检查网络或重启应用")), Symbol.Clear);
        }
        Loading = false;
    }


    private async Task<List<EventContentSideWrapper>?> FormatSideDataAsync(SideModule sideModules)
    {
        if (sideModules.Content is JsonElement jsonElement)
        {
            var jsonObject = jsonElement.Deserialize<EventContentSide>(WikiContext.Default.EventContentSide);
            List<EventContentSideWrapper> wrappers = new();
            foreach (var tag in jsonObject!.Tabs)
            {
                EventContentSideWrapper wrapper = new();
                wrapper.Title = tag.Name;
                wrapper.ImgMode = tag.ImgMode;
                if (DateTime.TryParse(tag.CountDown.DateRange[0], out var time) && DateTime.TryParse(tag.CountDown.DateRange[1], out var endTime))
                {
                    wrapper.StartTime = time;
                    wrapper.StopTime = endTime;
                }
                wrapper.Image1 = tag.Images[0].Image;
                wrapper.Image2 = tag.Images[1].Image;
                wrapper.Image3 = tag.Images[2].Image;
                wrapper.Image4 = tag.Images[3].Image;
                wrapper.Cali();
                wrappers.Add(wrapper);
            }
            return wrappers;
        }
        else
            return [];
    }


    [RelayCommand]
    async Task OpenDataCenter()
    {
        OpenKuroCommunityWindow(await CreateDataCenterSessionContext());
    }

    [RelayCommand]
    async Task OpenGrowthCalculator()
    {
        OpenKuroCommunityWindow(await CreateGrowthCalculatorSessionContext());
    }

    [RelayCommand]
    async Task OpenResourceBriefing()
    {
        OpenKuroCommunityWindow(await CreateResourceBriefingSessionContext());
    }

    [RelayCommand]
    async Task OpenCalendar()
    {
        OpenKuroCommunityWindow(await CreateCalendarSessionContext());
    }

    [RelayCommand]
    async Task OpenMap()
    {
        OpenKuroCommunityWindow(CreateMapSessionContext());
    }

    [RelayCommand]
    async Task OpenGameSign()
    {
        var win = Instance.Host.Services.GetRequiredService<IViewFactorys>()!.ShowSignWindow(this.SelectGamer);
        win.ApplyWindowsOption(SignWindowOption);
        win.ExtendsContentIntoTitleBar = true;
        win.AppWindow.Show();
    }


    async partial void OnSelectGamerChanged(GameRoilDataItem value)
    {
        if (value == null)
            return;
        await RefreshBaseData(value);
    }

    private async Task RefreshBaseData(GameRoilDataItem value)
    {
        var account = AccountService.CurrentAccount;
        if (account is not null)
            await WavesClient.UpdateRefreshToken(account, value);
    }

    [RelayCommand]
    private async Task RefreshUserAsync()
    {
        try
        {
            this.SelectGamer = null;
            var account = AccountService.CurrentAccount;
            if (account is not null && await WavesClient.IsLoginAsync(account, CTS.Token))
            {
                var roles = await TryInvokeAsync(async () =>
                    await WavesClient.GetGamerAsync(account, Waves.Core.Models.Enums.GameType.Waves, this.CTS.Token)
                );
                if (roles.Code != 0)
                {
                    TipShow.ShowMessage(LanguageService.FormatByText(LanguageService.GetStringByText("获取数据失败，请检查网络或重启应用")), Symbol.Clear);
                    return;
                }
                this.Gamers = roles.Result.Data.ToObservableCollection();
                this.SelectGamer = Gamers[0];
                this.KuroLogin = true;
            }
        }
        catch (Exception ex)
        {

            TipShow.ShowMessage(LanguageService.FormatByText(LanguageService.GetStringByText("刷新失败:{0}"), ex.Message), Symbol.Accept);
        }
    }

    public override void Dispose()
    {
        Actives.Clear();
        Actives = null;
        WeaponActive?.Clear();
        RoleActive?.Clear();
        WeaponActive = null;
        RoleActive = null;
        base.Dispose();
    }

    private void OpenKuroCommunityWindow(WebSessionContext? context)
    {
        if (context is null)
        {
            return;
        }
        KuroDataCenterWindow window = new KuroDataCenterWindow( context, CommunityWindowOption);
        if(window.Content is FrameworkElement element)
        {
            element.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
        }
        window.AppWindow.Show();
    }

    private async Task<WebSessionContext?> CreateDataCenterSessionContext()
    {
        return await CreateCommunitySessionContext(WebSessionContext.CreateDataCenter);
    }

    private async Task<WebSessionContext?> CreateGrowthCalculatorSessionContext()
    {
        return await CreateCommunitySessionContext(WebSessionContext.CreateGrowthCalculator);
    }

    private async Task<WebSessionContext?> CreateResourceBriefingSessionContext()
    {
        return await CreateCommunitySessionContext(WebSessionContext.CreateResourceBriefing);
    }

    private async Task<WebSessionContext?> CreateCalendarSessionContext()
    {
        return await CreateCommunitySessionContext(WebSessionContext.CreateCalendar);
    }

    private WebSessionContext? CreateMapSessionContext()
    {
        var snapshot = CreateLoginSnapshot();
        if (snapshot is null || SelectGamer is null)
        {
            return null;
        }

        return WebSessionContext.CreateMap(
            snapshot,
            SelectGamer.ServerId,
            SelectGamer.RoleId,
            SelectGamer.ServerName,
            SelectGamer.RoleName);
    }

    private async Task<WebSessionContext?> CreateCommunitySessionContext(
        Func<KuroLoginSnapshot, string, string, string?, string?, WebSessionContext> factory)
    {
        var snapshot = CreateLoginSnapshot();
        if (snapshot is null || SelectGamer is null)
        {
            return null;
        }
        var current = this.KuroAccountService.CurrentAccount;
        if(current == null)
        {
            SystemEventMessager.Publish(new()
            {
                Message = LanguageService.GetStringByText("请选择一个账号"),
                Delay = TimeSpan.FromSeconds(10).TotalSeconds
            });
            return null;
        }
        var refreshData = await this.KuroClient.RefreshGamerDataAsync(current, SelectGamer, this.CTS.Token);
        if (refreshData == null || !refreshData.Success)
        {
            SystemEventMessager.Publish(new()
            {
                Message = LanguageService.GetStringByText("当前账号异常，请重新登录"),
                Delay = TimeSpan.FromSeconds(10).TotalSeconds
            });
            return null;
        }
        return factory(
            snapshot,
            SelectGamer.ServerId,
            SelectGamer.RoleId,
            SelectGamer.ServerName,
            SelectGamer.RoleName);
    }

    private KuroLoginSnapshot? CreateLoginSnapshot()
    {
        var session = AccountService.Current;
        if (session is null)
        {
            return null;
        }

        return new KuroLoginSnapshot
        {
            Token = session.Token ?? string.Empty,
            Did = session.TokenDid ?? string.Empty,
            UserId = session.TokenId ?? string.Empty,
            AppVersion = App.AppVersion,
            ChannelId = "8",
            EnterSource = "12",
            UserAgentName = "KuroGameBox",
            Os = "Android",
        };
    }
}
