using Haiyu.ViewModel.WikiViewModels;

namespace Haiyu.Pages.GameWikis;

public sealed partial class WavesWikiPage : Page, IPage,IDisposable
{
    public WavesWikiPage()
    {
        InitializeComponent();
        this.ViewModel = Instance.GetService<WavesWikiViewModel>();
    }

    public WavesWikiViewModel ViewModel { get; private set; }
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        this.ViewModel.Dispose();
        base.OnNavigatedFrom(e);
    }

    public void Dispose()
    {
        this.ViewModel.Dispose();
    }

    public Type PageType => typeof(WavesWikiPage);
}