using Haiyu.ViewModel.WikiViewModels;


namespace Haiyu.Pages.GameWikis;

public sealed partial class PunishWikiPage : Page, IPage,IDisposable
{
    public PunishWikiPage()
    {
        InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<PunishWikiViewModel>();
    }
    public PunishWikiViewModel ViewModel { get; private set; }
    public Type PageType => typeof(PunishWikiPage);

    public void Dispose()
    {
        this.ViewModel.Dispose();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        this.ViewModel.Dispose();
        base.OnNavigatedFrom(e);
    }
}
