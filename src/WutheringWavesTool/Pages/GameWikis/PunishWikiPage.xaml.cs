using Haiyu.ViewModel.WikiViewModels;


namespace Haiyu.Pages.GameWikis;

public sealed partial class PunishWikiPage : Page, IPage,IDisposable
{
    public PunishWikiPage()
    {
        InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<PunishWikiViewModel>();
    }
    public PunishWikiViewModel? ViewModel { get; private set; }
    public Type PageType => typeof(PunishWikiPage);

    public void Dispose()
    {
        try
        {
            this.ViewModel?.Dispose();
        }
        finally
        {
            this.ViewModel = null;
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        try
        {
            this.Bindings.StopTracking();
            this.ViewModel?.Dispose();
        }
        finally
        {
            this.ViewModel = null;
            base.OnNavigatedFrom(e);
        }
    }
}
