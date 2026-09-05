using Haiyu.Common.Contracts;

namespace Haiyu.Pages.Dialogs;

public sealed partial class WebGameLogin : ContentDialog, IDialog
{
    public WebGameLogin(
        WebGameViewModel viewModel,
        IThemeService themeService
    )
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = themeService.CurrentTheme;
    }

    public WebGameViewModel ViewModel { get; }

    public void SetData(object data)
    {
    }
}
