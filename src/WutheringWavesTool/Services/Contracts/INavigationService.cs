namespace Haiyu.Services.Contracts;

public interface INavigationService
{
    public void RegisterView(Microsoft.UI.Xaml.Controls.Frame frame);

    public Microsoft.UI.Xaml.Controls.Frame Frame { get; }

    public void UnRegisterView();

    public bool CanGoBack { get; }

    public bool CanGoForward { get; }

    public bool GoBack();

    public bool GoForward();

    public bool NavigationTo(string key, object args, NavigationTransitionInfo transition);

    public bool NavigationTo<ViewModel>(object args, NavigationTransitionInfo transition)
        where ViewModel : ObservableObject;

    public event NavigatedEventHandler Navigated;

    public event NavigationFailedEventHandler NavigationFailed;

    public void ClearHistory();
}
