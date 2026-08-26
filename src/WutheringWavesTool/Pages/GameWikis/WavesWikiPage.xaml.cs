using Haiyu.ViewModel.WikiViewModels;

namespace Haiyu.Pages.GameWikis;

public sealed partial class WavesWikiPage : Page, IPage,IDisposable
{
    public WavesWikiPage()
    {
        InitializeComponent();
        this.ViewModel = Instance.GetService<WavesWikiViewModel>();
    }

    public WavesWikiViewModel? ViewModel { get; private set; }
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

    public Type PageType => typeof(WavesWikiPage);
}