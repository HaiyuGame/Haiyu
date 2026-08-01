using System.Threading.Tasks;
using Haiyu.ServiceHost;
using Haiyu.ServiceHost.XBox.Commons;
using Haiyu.Services;
using Haiyu.Services.DialogServices;
using Waves.Settings;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class GameEnhancedViewModel : DialogViewModelBase
{
    public XBoxService XBoxService { get; }
    public XBoxConfig XboxConfig { get; }
    public ITipShow TipShow { get; }

    public GameEnhancedViewModel(
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager,
        XBoxService xboxService,
        XBoxConfig xBoxConfig,
        ITipShow tipShow
    )
        : base(dialogManager)
    {
        TipShow = tipShow;
        this.XBoxService = xboxService;
        XboxConfig = xBoxConfig;
    }

    [ObservableProperty]
    public partial bool? XboxEnable { get; set; }

    /// <summary>
    /// 开启XBox 适配
    /// </summary>
    [RelayCommand]
    async Task EnableConfig(string Tag)
    {
        if (Tag == "Fps")
        {
            await XboxConfig.SetFpsEnableAsync(this.XboxEnable ?? false,this.CTS.Token);
        }
        if (Tag == "Xbox")
        {
            await XboxConfig.SetIsEnableAsync(XboxEnable ?? false);
            if (XboxEnable == true)
            {
                await XBoxService.StartAsync();
            }
            else
            {
                await XBoxService.StopAsync();
            }
        }
    }
}
