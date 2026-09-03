

using Haiyu.Common.Contracts;

namespace Haiyu.Pages.Dialogs
{
    public sealed partial class KuroGameSettingDialog : ContentDialog, IDialog
    {
        public KuroGameSettingDialog()
        {
            InitializeComponent();
            this.ViewModel = Instance.Host.Services.GetRequiredService<KuroGameSettingViewModel>();
            this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
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
