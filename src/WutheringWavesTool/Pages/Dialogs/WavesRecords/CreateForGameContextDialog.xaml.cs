using Haiyu.Common.Contracts;
using Haiyu.Models.Dialogs;
using Haiyu.ViewModel.DialogViewModels.WavesRecords;
using Waves.Settings;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Haiyu.Pages.Dialogs.WavesRecords
{
    public sealed partial class CreateForGameContextDialog : ContentDialog, IDialog
    {
        public CreateForGameContextDialog(CreateForGameContextViewModel viewModel)
        {
            this.InitializeComponent();
            this.ViewModel = viewModel;
            this.RequestedTheme = Instance
                .Host.Services.GetRequiredService<IThemeService>()
                .CurrentTheme;
        }

        public CreateForGameContextViewModel ViewModel { get; }

        public void SetData(object data) 
        {
            if(data is string contextName)
            {
                this.ViewModel.ContextName = contextName;
            }
        }
    }
}
