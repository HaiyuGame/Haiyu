using CommunityToolkit.WinUI.Controls;
using Haiyu.Common.Contracts;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Haiyu.Pages.Dialogs;

public sealed partial class CloudGameSettingDialog : ContentDialog,IDialog
{
    public CloudGameSettingDialog()
    {
        InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<CloudGameSettingViewModel>();
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public CloudGameSettingViewModel ViewModel { get; }

    public void SetData(object data)
    {
        if(data is GameType type)
        {
            this.ViewModel.CloudGameContext = type switch
            {
                GameType.Waves => Instance.Host.Services.GetRequiredKeyedService<IKuroCloudGameContext>(nameof(KuroCloudGameContext)),
                _ => Instance.Host.Services.GetRequiredKeyedService<IKuroCloudGameContext>(nameof(KuroCloudGameContext))
            };  
        }
    }
}
