using Haiyu.ViewModel.GameViewModels;

namespace Haiyu.Pages.GamePages;

public sealed partial class WavesCloudGamePage : Page,IPage
{
    public WavesCloudGamePage()
    {
        InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<WavesCloudGameViewModel>();
    }

    public Type PageType => typeof(WavesCloudGamePage);

    public WavesCloudGameViewModel ViewModel { get; }
}
