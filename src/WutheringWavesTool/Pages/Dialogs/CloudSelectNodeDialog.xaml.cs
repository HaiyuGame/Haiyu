using Haiyu.Common.Contracts;
using Waves.Api.Models.CloudGame;
using Waves.Core.Models.CloudGame;

namespace Haiyu.Pages.Dialogs
{
    public sealed partial class CloudSelectNodeDialog : ContentDialog,IResultDialog<LauncheNodeConfig>
    {
        public CloudSelectNodeDialog(
        CloudSelectNodeViewModel viewModel,
        IThemeService themeService
    )
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = themeService.CurrentTheme;
    }

        public CloudSelectNodeViewModel ViewModel { get; }


        public void SetData(object data)
        {
            if(data is string strValue)
            {
                this.ViewModel.Id = strValue;
            }
        }
    }
}
