using Haiyu.ServiceHost;
using Haiyu.ServiceHost.XBox.Commons;
using Haiyu.Services;
using Haiyu.Services.DialogServices;
using System.Threading.Tasks;
using Waves.Core.Settings;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class GameEnhancedViewModel : DialogViewModelBase
{
    public XBoxController XboxGameController { get; }
    public XBoxConfig XboxConfig { get; }
    public ITipShow TipShow { get; }

    public GameEnhancedViewModel([FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager, XBoxController xBoxController, XBoxConfig xBoxConfig,ITipShow tipShow)
        : base(dialogManager)
    {
        TipShow = tipShow;
        XboxGameController = xBoxController;
        XboxConfig = xBoxConfig;
    }

    /// <summary>
    /// 开启XBox 适配
    /// </summary>
    [RelayCommand]

    async Task EnableConfig(RoutedEventArgs e)
    {
        if (e.OriginalSource is CheckBox box)
        {
            
            if (SystemHelper.IsAdministrator())
            {
                if (box.Tag.ToString() == "Fps")
                {
                    XboxConfig.FpsEnable = box.IsChecked ?? false;
                }
                if (box.Tag.ToString() == "Xbox")
                {
                    XboxConfig.IsEnable = box.IsChecked ?? false;
                }
            }
            else
            {
                if (box.Tag.ToString() == "Fps")
                    XboxConfig.FpsEnable = false;
                if (box.Tag.ToString() == "Xbox")
                    XboxConfig.IsEnable = false;
                box.IsChecked = false;
                await TipShow.ShowMessageAsync("请使用管理员模式启动Haiyu",Symbol.Clear);
            }
        }
    }

    

}
