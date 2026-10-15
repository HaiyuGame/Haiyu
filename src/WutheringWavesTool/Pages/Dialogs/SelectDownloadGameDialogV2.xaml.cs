using Haiyu.Common.Contracts;

namespace Haiyu.Pages.Dialogs;

public sealed partial class SelectDownloadGameDialogV2 : ContentDialog, IDialog
{
    public SelectDownloadGameDialogV2(SelectDownloadGameViewModelV2 viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public SelectDownloadGameViewModelV2 ViewModel { get; }

    public void SetData(object data) => ViewModel.SetData(data);
}
