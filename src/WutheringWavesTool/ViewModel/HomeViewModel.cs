using Haiyu.ViewModel.WikiViewModels;

using Haiyu.Helpers;

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
        LanguageService.GetString("WutheringName")!,
        LanguageService.GetString("PunishName")!,
    };

    [ObservableProperty]
    public partial string SelectTab { get; set; }

    [RelayCommand]
    Task Loaded() =>
        RunWhileAliveAsync(_ =>
        {
            this.SelectTab = Tabs.First();
            return Task.CompletedTask;
        });

    protected override void OnDisposing()
    {
        NavigationService.UnRegisterView();
    }

    partial void OnSelectTabChanged(string value)
    {
        switch (value)
        {
            case var punishName when punishName == LanguageService.GetString("PunishName"):
                this.NavigationService.NavigationTo<PunishWikiViewModel>(null, new DrillInNavigationTransitionInfo());
                break;
            case var wutheringName when wutheringName == LanguageService.GetString("WutheringName"):
                this.NavigationService.NavigationTo<WavesWikiViewModel>(null, new DrillInNavigationTransitionInfo());
                break;
        }
    }
}
