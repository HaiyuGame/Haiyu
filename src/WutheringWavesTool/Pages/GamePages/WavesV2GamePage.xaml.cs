using Haiyu.ViewModel.GameViewModels.GameContexts;

namespace Haiyu.Pages.GamePages;

public sealed partial class WavesV2GamePage : Page,IPage
{
    public WavesV2GamePage()
    {
        InitializeComponent();
        ViewModel = Instance.Host.Services.GetRequiredService<WavesV2GameContextViewModel>();
    }

    public WavesV2GameContextViewModel ViewModel { get; set; }

    public Type PageType => typeof(WavesV2GamePage);

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        this.Bindings.StopTracking();
        base.OnNavigatedTo(e);
        GC.Collect();
    }
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {

    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
    }
}
