

using Haiyu.Common.Contracts;

namespace Haiyu.Pages.Dialogs
{
    public sealed partial class KuroGameSettingDialog : ContentDialog, IDialog
    {
        public KuroGameSettingDialog(
        KuroGameSettingViewModel viewModel,
        IThemeService themeService
    )
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = themeService.CurrentTheme;
    }

        public KuroGameSettingViewModel ViewModel { get; }

        public void SetData(object data)
        {
            if(data is GameSettingDialogConfig config)
            {
                this.ViewModel.SetConfig(config);
            }
        }
    }
}
