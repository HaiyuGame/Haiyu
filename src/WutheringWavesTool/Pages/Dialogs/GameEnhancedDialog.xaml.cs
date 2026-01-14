using Haiyu.ServiceHost;

namespace Haiyu.Pages.Dialogs;

public sealed partial class GameEnhancedDialog : ContentDialog,IDialog
{
    public GameEnhancedDialog()
    {
        InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<GameEnhancedViewModel>();
    }

    public GameEnhancedViewModel ViewModel { get; }

    public void SetData(object data)
    {
        if (SystemHelper.IsAdministrator())
        {
            this.xboxEnable.IsChecked = ViewModel.XboxConfig.IsEnable;
        }
        else
        {
            this.xboxEnable.IsChecked = false;
        }
    }
}
