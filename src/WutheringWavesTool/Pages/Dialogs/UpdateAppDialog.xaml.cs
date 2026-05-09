using Haiyu.Plugin.Models;


namespace Haiyu.Pages.Dialogs;

public sealed partial class UpdateAppDialog : ContentDialog,IDialog
{
    public UpdateAppDialog()
    {
        InitializeComponent();
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
