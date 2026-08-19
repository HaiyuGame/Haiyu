using Haiyu.ViewModel.GameViewModels.GameContexts;

namespace Haiyu.Pages.GamePages;

public sealed partial class WavesV2GamePage : Page,IPage
{
    public WavesV2GamePage()
    {
        InitializeComponent();
        ViewModel = Instance.Host.Services.GetRequiredService<WavesV2GameContextViewModel>();
    }

    public WavesV2GameContextViewModel? ViewModel { get; set; }

    public Type PageType => typeof(WavesV2GamePage);

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
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {

    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        switcher.Switch();
        if (switcher.CurrentIndex == 1)
        {
            filpViewAutoPlay.IsPlay = true;
        }
        else
        {
            filpViewAutoPlay.IsPlay = false;
        }
    }
}
