using Haiyu.Common.Contracts;

namespace Haiyu.Pages.Dialogs;

public sealed partial class GrayMenuDialog : ContentDialog, IDialog
{
    public GrayMenuDialog(GrayMenuViewModel viewModel)
    {
        this.InitializeComponent();
        this.RequestedTheme = Instance
            .Host.Services.GetRequiredService<IThemeService>()
            .CurrentTheme;
        ViewModel = viewModel;
    }

    public GrayMenuViewModel ViewModel { get; }

    public void SetData(object data) { }
}
