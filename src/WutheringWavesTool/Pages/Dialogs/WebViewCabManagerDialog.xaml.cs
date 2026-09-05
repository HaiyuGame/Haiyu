using Haiyu.Common.Contracts;

namespace Haiyu.Pages.Dialogs;

public sealed partial class WebViewCabManagerDialog : ContentDialog, IDialog
{
    public WebViewCabManagerDialog(
        WebViewCabManagerViewModel viewModel,
        IThemeService themeService
    )
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = themeService.CurrentTheme;
    }

    public WebViewCabManagerViewModel ViewModel { get; }

    public void SetData(object data)
    {
    }
}
