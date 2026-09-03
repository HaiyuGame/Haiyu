using Haiyu.Common.Contracts;

namespace Haiyu.Pages.Dialogs;

public sealed partial class WebGameLogin : ContentDialog, IDialog
{
    public WebGameLogin()
    {
        InitializeComponent();
        this.ViewModel = Instance.GetService<WebGameViewModel>();
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public WebGameViewModel ViewModel { get; }

    public void SetData(object data)
    {
    }
}
