using Haiyu.Common.Contracts;

namespace Haiyu.Pages.Dialogs;

public sealed partial class WebViewCabManagerDialog : ContentDialog, IDialog
{
    public WebViewCabManagerDialog()
    {
        InitializeComponent();
        ViewModel = Instance.Host.Services.GetRequiredService<WebViewCabManagerViewModel>();
        RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public WebViewCabManagerViewModel ViewModel { get; }

    public void SetData(object data)
    {
    }
}
