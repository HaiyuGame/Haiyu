using Haiyu.ViewModel.WikiViewModels;

namespace Haiyu.ViewModel;

public partial class HomeViewModel : ViewModelBase
{
    public HomeViewModel(
        IWallpaperService wallpaperService,
        [FromKeyedServices(nameof(GameWikiNavigationService))] INavigationService navigationService
    )
    {
        WallpaperService = wallpaperService;
        NavigationService = navigationService;
    }

    public IWallpaperService WallpaperService { get; }
    public INavigationService NavigationService { get; }

    [ObservableProperty]
    public partial ObservableCollection<string> Tabs { get; set; } = new()
    {
        "鸣潮",
        "战双帕弥什",
    };

    [ObservableProperty]
    public partial string SelectTab { get; set; }

    [RelayCommand]
    async Task Loaded()
    {
        this.SelectTab = Tabs.First();
    }

    partial void OnSelectTabChanged(string value)
    {
        switch (value)
        {
            case "战双帕弥什":
                this.NavigationService.NavigationTo<PunishWikiViewModel>(null, new DrillInNavigationTransitionInfo());
                break;
            case "鸣潮":
                this.NavigationService.NavigationTo<WavesWikiViewModel>(null, new DrillInNavigationTransitionInfo());
                break;
        }
    }
}
