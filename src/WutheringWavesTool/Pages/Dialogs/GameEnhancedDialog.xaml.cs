using Haiyu.Common.Contracts;
using Haiyu.ServiceHost;

namespace Haiyu.Pages.Dialogs;

public sealed partial class GameEnhancedDialog : ContentDialog, IDialog
{
    public GameEnhancedDialog(
        GameEnhancedViewModel viewModel,
        IThemeService themeService
    )
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = themeService.CurrentTheme;
    }

    public GameEnhancedViewModel ViewModel { get; }

    public async void SetData(object data)
    {
        this.xboxEnable.IsChecked = await ViewModel.XboxConfig.GetIsEnableAsync(
            this.ViewModel.CTS.Token
        );
    }

    private async void xboxEnable_Checked(object sender, RoutedEventArgs e)
    {
        await ViewModel.XboxConfig.SetIsEnableAsync(true, this.ViewModel.CTS.Token);
    }

    private async void xboxEnable_Unchecked(object sender, RoutedEventArgs e)
    {
        await ViewModel.XboxConfig.SetIsEnableAsync(false, this.ViewModel.CTS.Token);
    }
}
