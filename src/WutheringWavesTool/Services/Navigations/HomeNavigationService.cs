using Haiyu.Common.Contracts;

namespace Haiyu.Services.Navigations;

public class HomeNavigationService : NavigationServiceBase
{
    public HomeNavigationService(IPageService pageService)
        : base(pageService) { }

    public override bool NavigationTo(
        string key,
        object args,
        NavigationTransitionInfo transitionInfo
    )
    {
        var pageType = PageService.GetPage(key);
        if (pageType == null)
            return false;
        if (RootFrame.Content is IPage OrginpageType)
        {
            if (
                RootFrame != null
                && (OrginpageType.PageType != pageType || args != null && !args.Equals(Paramter))
            )
            {
                Paramter = args;
                return RootFrame.Navigate(pageType, Paramter, transitionInfo);
            }
        }
        else if (RootFrame.Content == null)
        {
            Paramter = args;
            return RootFrame.Navigate(pageType, Paramter, new DrillInNavigationTransitionInfo());
        }
        return false;
    }
}
