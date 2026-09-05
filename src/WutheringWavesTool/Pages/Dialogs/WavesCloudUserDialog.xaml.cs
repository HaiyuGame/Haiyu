

using Haiyu.Common.Contracts;

namespace Haiyu.Pages.Dialogs
{
    public sealed partial class WavesCloudUserDialog : ContentDialog, IDialog
    {
        public WavesCloudUserDialog(
        WavesCloudUserViewModel viewModel,
        IThemeService themeService
    )
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = themeService.CurrentTheme;
    }

        public WavesCloudUserViewModel ViewModel { get; }

        public void SetData(object data)
        {

        }
    }
}
