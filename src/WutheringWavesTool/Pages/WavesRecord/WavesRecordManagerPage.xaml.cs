using Haiyu.Common.Contracts;
using Haiyu.ViewModel.WavesRecordManager;

namespace Haiyu.Pages.WavesRecord;

public sealed partial class WavesRecordManagerPage : Page, IWindowInitializable
{
    public WavesRecordManagerPage(WavesRecordManagerViewModel viewModel)
    {
        InitializeComponent();
        this.ViewModel = viewModel;
        this.RequestedTheme = Instance
            .Host.Services.GetRequiredService<IThemeService>()
            .CurrentTheme;
    }

    public WavesRecordManagerViewModel ViewModel { get; private set; }

    public void Dispose() { }

    public void Initialize()
    {
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        this.titleBar.Window = this.ViewModel.WinSession.Context.GetWindow();
    }
}
