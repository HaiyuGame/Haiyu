namespace Haiyu.Services.Navigations.Base;

public class NavigationServiceBase : INavigationService
{
    public NavigationServiceBase(IPageService pageService)
    {
        PageService = pageService;
    }

    public object Paramter { get; set; }

    public IPageService PageService { get; }

    public Microsoft.UI.Xaml.Controls.Frame RootFrame { get; private set; }

    public bool CanGoBack => RootFrame != null ? RootFrame.CanGoBack : false;

    public bool CanGoForward => RootFrame != null ? RootFrame.CanGoForward : false;

    public Microsoft.UI.Xaml.Controls.Frame Frame => RootFrame;

    public event NavigatedEventHandler Navigated;
    public event NavigationFailedEventHandler NavigationFailed;

    public bool GoBack()
    {
        if (RootFrame.CanGoBack)
        {
            ElementSoundPlayer.Play(ElementSoundKind.GoBack);
            RootFrame.GoBack();
            return true;
        }
        return false;
    }

    public bool GoForward()
    {
        if (RootFrame.CanGoForward)
        {
            RootFrame.GoForward();
            return true;
        }
        return false;
    }

    public virtual bool NavigationTo(
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
            return RootFrame.Navigate(pageType, Paramter, transitionInfo);
        }
        return false;
    }

    public void RegisterView(Microsoft.UI.Xaml.Controls.Frame frame)
    {
        RootFrame = frame;
        RootFrame.Navigated += Navigated;
        RootFrame.NavigationFailed += NavigationFailed;
    }

    public void UnRegisterView()
    {
        if (RootFrame == null)
            return;

        RootFrame.Navigated -= Navigated;
        RootFrame.NavigationFailed -= NavigationFailed;
        ClearHistory();
        RootFrame = null;
    }

    public bool NavigationTo<ViewModel>(object args, NavigationTransitionInfo transitionInfo)
        where ViewModel : ObservableObject =>
        NavigationTo(typeof(ViewModel).FullName, args, transitionInfo);

    public void ClearHistory()
    {
        if (RootFrame == null)
            return;
        RootFrame.BackStack.Clear();
        RootFrame.ForwardStack.Clear();
    }
}
