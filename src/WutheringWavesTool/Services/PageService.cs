using Haiyu.Common.Contracts;
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
        this.RegisterView<HomePage, HomeViewModel>();
        #region GameContext
        this.RegisterView<PunishV2GamePage, PunishV2GameContextViewModel>();
        this.RegisterView<WavesV2GamePage, WavesV2GameContextViewModel>();
        this.RegisterView<WavesCloudGamePage, WavesCloudGameViewModel>();
        #endregion

        #region Wiki
        this.RegisterView<WavesWikiPage, WavesWikiViewModel>();
        this.RegisterView<PunishWikiPage, PunishWikiViewModel>();
        #endregion

        #region OOBE
        this.RegisterView<LanguageSelectPage, LanguageSelectViewModel>();
        #endregion

        this.RegisterView<ToolkitPage, ToolkitViewModel>();
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
            throw new ArgumentException(LanguageService.GetStringByText("已注册ViewModel"));
        }
        if (_pages.ContainsValue(typeof(View)))
        {
            throw new ArgumentException(LanguageService.GetStringByText("已注册View"));
        }
        _pages.Add(key: typeof(ViewModel).FullName, typeof(View));
    }
}
