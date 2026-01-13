using Haiyu.ServiceHost.XBox.Commons;
using Haiyu.Services;
using Haiyu.Services.DialogServices;
using Waves.Core.Settings;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class GameEnhancedViewModel : DialogViewModelBase
{
    public XboxGameController XboxGameController { get; }
    public XBoxConfig XboxConfig { get; }
    public GameEnhancedViewModel([FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager, XBoxController xBoxController, XBoxConfig xBoxConfig)
        :base(dialogManager)
    {
        
    }
}
