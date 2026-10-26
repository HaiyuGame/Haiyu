using Haiyu.Common.Contracts;

namespace Haiyu.Pages.WavesRecord;

public sealed partial class WavesRecordManagerPage : Page
{

    public WavesRecordManagerPage(DeviceInfoViewModel viewModel)
    {
        InitializeComponent();
        this.ViewModel = viewModel;
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public DeviceInfoViewModel? ViewModel { get; private set; }

}
