

using Haiyu.Common.Contracts;

namespace Haiyu.Pages.Dialogs
{
    public sealed partial class WavesCloudUserDialog : ContentDialog, IDialog
    {
        public WavesCloudUserDialog()
        {
            InitializeComponent();
            this.ViewModel = Instance.Host.Services.GetRequiredService<WavesCloudUserViewModel>();
            this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
        }

        public WavesCloudUserViewModel ViewModel { get; }

        public void SetData(object data)
        {

        }
    }
}
