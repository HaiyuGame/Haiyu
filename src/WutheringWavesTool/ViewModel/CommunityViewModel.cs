using Haiyu.Services.DialogServices;

namespace Haiyu.ViewModel;

public partial class CommunityViewModel : ViewModelBase, IDisposable
{
    public CommunityViewModel(
        IKuroClient wavesClient,
        IViewFactorys viewFactorys,
        [FromKeyedServices(nameof(CommunityNavigationService))]
            INavigationService navigationService,
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager
    )
    {
        WavesClient = wavesClient;
        ViewFactorys = viewFactorys;
        NavigationService = navigationService;
        DialogManager = dialogManager;
        RegisterMessanger();
    }

    public IKuroClient WavesClient { get; }
    public IAppContext<App> AppContext { get; }
    public IViewFactorys ViewFactorys { get; }
    public INavigationService NavigationService { get; set; }
    public IDialogManager DialogManager { get; }

    [ObservableProperty]
    public partial bool IsLogin { get; set; }

    [ObservableProperty]
    public partial List<CommunitySwitchPageWrapper> Pages { get; set; } =
        CommunitySwitchPageWrapper.GetDefault();

    [ObservableProperty]
    public partial CommunitySwitchPageWrapper SelectPageItem { get; set; }

    [ObservableProperty]
    public partial bool DataLoad { get; set; } = false;

    public GameRoilDataItem Item { get; set; }
    private void RegisterMessanger()
    {
        this.Messenger.Register<SelectUserMessanger>(this, LoginMessangerMethod);
        this.Messenger.Register<UnLoginMessager>(this, UnLoginMethod);
        this.Messenger.Register<ShowRoleData>(this, ShowRoleMethod);
    }

    private async void UnLoginMethod(object recipient, UnLoginMessager message)
    {
        await LoadedAsync();
    }

    partial void OnSelectPageItemChanged(CommunitySwitchPageWrapper value)
    {
        switch (value.Tag.ToString())
        {
            case "DataGamer":
                NavigationService.NavigationTo<GameRoilsViewModel>(
                    Item,
                    new Microsoft.UI.Xaml.Media.Animation.DrillInNavigationTransitionInfo()
                );
                break;
            case "DataDock":
                NavigationService.NavigationTo<GamerDockViewModel>(
                    Item,
                    new Microsoft.UI.Xaml.Media.Animation.DrillInNavigationTransitionInfo()
                );
                break;
            case "DataChallenge":
                NavigationService.NavigationTo<GamerChallengeViewModel>(
                    Item,
                    new Microsoft.UI.Xaml.Media.Animation.DrillInNavigationTransitionInfo()
                );
                break;
            case "DataAbyss":
                NavigationService.NavigationTo<GamerTowerViewModel>(
                    null,
                    new Microsoft.UI.Xaml.Media.Animation.DrillInNavigationTransitionInfo()
                );
                break;
            case "DataWorld":
                NavigationService.NavigationTo<GamerExploreIndexViewModel>(
                    Item,
                    new Microsoft.UI.Xaml.Media.Animation.DrillInNavigationTransitionInfo()
                );
                break;
            case "Skin":
                NavigationService.NavigationTo<GamerSkinViewModel>(
                    Item,
                    new Microsoft.UI.Xaml.Media.Animation.DrillInNavigationTransitionInfo()
                );
                break;
            case "Boss2":
                NavigationService.NavigationTo<GamerSlashDetailViewModel>(
                    Item,
                    new Microsoft.UI.Xaml.Media.Animation.DrillInNavigationTransitionInfo()
                );
                break;
            case "Resource":
                NavigationService.NavigationTo<ResourceBriefViewModel>(
                    Item,
                    new Microsoft.UI.Xaml.Media.Animation.DrillInNavigationTransitionInfo()
                );
                break;
        }
    }

    private void ShowRoleMethod(object recipient, ShowRoleData message)
    {
        ViewFactorys.ShowRolesDataWindow(message).Activate();
    }

    private async void LoginMessangerMethod(object recipient, SelectUserMessanger message)
    {
        await LoadedAsync();
    }

    [RelayCommand]
    async Task LoadedAsync(Frame frame = null)
    {
        if (frame != null)
            this.NavigationService.RegisterView(frame);
        this.IsLogin = (await WavesClient.IsLoginAsync());
        if (!IsLogin)
            return;
        this.SelectPageItem = Pages[0];
        this.DataLoad = true;
    }

    public override void Dispose()
    {
        this.Messenger.UnregisterAll(this);
        this.NavigationService.UnRegisterView();
        this.NavigationService = null;
        this.CTS.Cancel();
        GC.SuppressFinalize(this);
    }
}
