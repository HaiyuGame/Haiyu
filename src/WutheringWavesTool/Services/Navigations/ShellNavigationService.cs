using Haiyu.Common.Contracts;

namespace Haiyu.Services.Navigations;

public class ShellNavigationService : NavigationServiceBase
{
    public ShellNavigationService(IPageService pageService)
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
                && (OrginpageType.PageType != pageType || args != null && !args.Equals(Parameter))
            )
            {
                Parameter = args;
                return RootFrame.Navigate(pageType, Parameter, transitionInfo);
            }
        }
        else if (RootFrame.Content == null)
        {
            Parameter = args;
            return RootFrame.Navigate(pageType, Parameter, new DrillInNavigationTransitionInfo());
        }
        return false;
    }
}
