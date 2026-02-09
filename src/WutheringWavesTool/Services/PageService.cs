using Haiyu.Pages.GamePages;
using Haiyu.Pages.GameWikis;
using Haiyu.Pages.OOBE;
using Haiyu.ViewModel.GameViewModels;
using Haiyu.ViewModel.GameViewModels.GameContexts;
using Haiyu.ViewModel.OOBEViewModels;
using Haiyu.ViewModel.WikiViewModels;

namespace Haiyu.Services;

public sealed partial class PageService : IPageService
{
    private readonly Dictionary<string, Type> _pages;

    public PageService()
    {
        _pages = new();
        this.RegisterView<SettingPage, SettingViewModel>();
        this.RegisterView<CommunityPage, CommunityViewModel>();
        this.RegisterView<HomePage, HomeViewModel>();
        #region GameContext
        this.RegisterView<WavesGamePage, WavesGameContextViewModel>();
        this.RegisterView<PunishGamePage, PunishGameContextViewModel>();
        #endregion
        this.RegisterView<StatisticsPage, StatisticsViewModel>();
        this.RegisterView<GamerRoilsPage, GameRoilsViewModel>();
        this.RegisterView<PluginPage, PluginViewModel>();
        this.RegisterView<GamerDockPage, GamerDockViewModel>();
        this.RegisterView<CloudGamePage, CloudGameViewModel>();
        this.RegisterView<GamerChallengePage, GamerChallengeViewModel>();
        this.RegisterView<GamerSlashDetailPage, GamerSlashDetailViewModel>();
        this.RegisterView<GamerExploreIndexPage, GamerExploreIndexViewModel>();
        this.RegisterView<GamerTowerPage, GamerTowerViewModel>();
        this.RegisterView<GamerSkinPage, GamerSkinViewModel>();
        this.RegisterView<ResourceBriefPage, ResourceBriefViewModel>();

        this.RegisterView<RecordItemPage, RecordItemViewModel>();
        this.RegisterView<TestPage, TestViewModel>();
        #region Wiki
        this.RegisterView<WavesWikiPage, WavesWikiViewModel>();
        this.RegisterView<PunishWikiPage, PunishWikiViewModel>();
        #endregion

        #region OOBE
        this.RegisterView<LanguageSelectPage, LanguageSelectViewModel>();
        #endregion

    }

    public Type GetPage(string key)
    {
        _pages.TryGetValue(key, out var page);
        if (page is null)
        {
            return null;
        }
        return page;
    }

    public void RegisterView<View, ViewModel>()
        where View : Page, IPage
        where ViewModel : ObservableObject
    {
        var key = typeof(ViewModel).FullName;
        if (_pages.ContainsKey(key))
        {
            throw new ArgumentException("已注册ViewModel");
        }
        if (_pages.ContainsValue(typeof(View)))
        {
            throw new ArgumentException("已注册View");
        }
        _pages.Add(key: typeof(ViewModel).FullName, typeof(View));
    }
}
