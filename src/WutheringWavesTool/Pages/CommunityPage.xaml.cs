

namespace Haiyu.Pages;

public sealed partial class CommunityPage : Page, IPage, IDisposable,IWindowPage
{
    private bool disposedValue;

    public CommunityPage()
    {
        this.InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<CommunityViewModel>();
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (this.frame.Content is IDisposable disposable)
        {
            disposable.Dispose();
        }
        this.Dispose();
        GC.Collect();
        base.OnNavigatedFrom(e);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if(e.Parameter is GameRoilDataItem item)
        {
            this.ViewModel.Item = item;
        }
    }

    public Type PageType => typeof(CommunityPage);

    public CommunityViewModel ViewModel { get; private set; }
    public Window Window { get; private set; }

    private void dataSelect_SelectionChanged(
        SelectorBar sender,
        SelectorBarSelectionChangedEventArgs args
    )
    {
        if (sender.SelectedItem.Tag == null)
            return;
    }

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                this.ViewModel.Dispose();
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }



    public void SetData(object value)
    {
        if (value is GameRoilDataItem item)
        {
            this.ViewModel.Item = item;
            this.title.Title = $"{item.RoleName}-{item.ServerName}";
        }
    }

    public void SetWindow(Window window)
    {
        this.Window = window;
        this.Window.AppWindow.Closing += AppWindow_Closing;
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        Dispose();
        this.Window.AppWindow.Closing -= AppWindow_Closing;
    }
}
