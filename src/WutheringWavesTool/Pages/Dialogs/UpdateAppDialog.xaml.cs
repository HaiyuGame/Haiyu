using Haiyu.Common.Contracts;
using Haiyu.Plugin.Models;
using Waves.Settings;


namespace Haiyu.Pages.Dialogs;

public sealed partial class UpdateAppDialog : ContentDialog,IDialog
{
    public UpdateAppDialog(
        UpdateAppViewModel viewModel,
        IThemeService themeService
    )
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = themeService.CurrentTheme;
    }

    public UpdateAppViewModel ViewModel { get; }

    public void SetData(object data)
    {
        if(data is DisplayVersionInfo info)
        {
            this.ViewModel.SetInfo(info);
        }
    }

}
