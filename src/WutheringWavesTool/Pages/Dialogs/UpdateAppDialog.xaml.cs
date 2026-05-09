using Haiyu.Plugin.Models;
using Waves.Core.Settings;


namespace Haiyu.Pages.Dialogs;

public sealed partial class UpdateAppDialog : ContentDialog,IDialog
{
    public UpdateAppDialog()
    {
        InitializeComponent();
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
        this.ViewModel = Instance.Host.Services.GetRequiredService<UpdateAppViewModel>();
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
