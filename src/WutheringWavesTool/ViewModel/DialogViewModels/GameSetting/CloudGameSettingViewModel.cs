using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.Models.Enums;
using Waves.Core.Services;
using Waves.Core.Services.CloudGameServices;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class CloudGameSettingViewModel : DialogViewModelBase
{
    [ObservableProperty]
    public partial ObservableCollection<QualityWrapper> Qualitys { get; set; } =
        QualityWrapper.Create();

    [ObservableProperty]
    public partial ObservableCollection<int> Fps { get; set; } = [30, 60];

    [ObservableProperty]
    public partial int SelectFps { get; set; }
    
    [ObservableProperty]
    public partial QualityWrapper SelectQualitys { get; set; }

    [ObservableProperty]
    public partial bool Enable { get; set; }

    [ObservableProperty]
    public partial bool ShowNetworkState { get; set; }

    [ObservableProperty]
    public partial int CodeType { get; set; } = CloudGameMethod.DefaultCodecType;

    public IKuroCloudGameContext CloudGameContext { get; internal set; }

    public CloudGameSettingViewModel([FromKeyedServices(nameof(KuroCloudGameContext))]IKuroCloudGameContext  cloudGameContext)
    {
        this.CloudGameContext = cloudGameContext;
    }

    [RelayCommand]
    async Task Loaded()
    {
        //初始化画质设置
        if (
            Enum.TryParse<CloudQualityType>(
                (
                    await this.CloudGameContext.GameLocalConfig.GetConfigAsync(
                        CloudGameLocalSettingName.QualityType
                    )
                ),
                out var quality
            )
        )
        {
            foreach (var item in Qualitys)
            {
                if (item.Type == quality)
                {
                    this.SelectQualitys = item;
                    break;
                }
            }
        }

        if (
            int.TryParse(
                (
                    await this.CloudGameContext.GameLocalConfig.GetConfigAsync(
                        CloudGameLocalSettingName.Fps
                    )
                ),
                out var fps
            )
        )
        {
            this.SelectFps = fps;
        }

        if (
            bool.TryParse(
                await this.CloudGameContext.GameLocalConfig.GetConfigAsync(
                    CloudGameLocalSettingName.EnableImageEnhancement
                ),
                out var enable
            )
        )
        {
            this.Enable = enable;
        }


        if (
            bool.TryParse(
                await this.CloudGameContext.GameLocalConfig.GetConfigAsync(
                    CloudGameLocalSettingName.EnableNetworkPanel
                ),
                out var netWorkEnable
            )
        )
        {
            this.ShowNetworkState = enable;
        }


    }

    async partial void OnSelectQualitysChanged(QualityWrapper? value)
    {
        if (value == null || this.CloudGameContext == null)
            return;
        await CloudGameContext.GameLocalConfig.SaveConfigAsync(
            CloudGameLocalSettingName.QualityType,
            Enum.GetName(value.Type!),
            this.CTS.Token
        );
        SeedUpdateQuality();
    }

    async partial void OnSelectFpsChanged(int value)
    {
        if (value == 0 || value == null)
            return;
        await CloudGameContext.GameLocalConfig.SaveConfigAsync(
            CloudGameLocalSettingName.Fps,
            value.ToString(),
            this.CTS.Token
        );
        SeedUpdateQuality();
    }

    async partial void OnEnableChanged(bool value)
    {
        if (this.CloudGameContext == null)
            return;
        await CloudGameContext.GameLocalConfig.SaveConfigAsync(
            CloudGameLocalSettingName.EnableImageEnhancement,
            value.ToString(),
            this.CTS.Token
        );
        SeedUpdateQuality();
    }

    async partial void OnShowNetworkStateChanged(bool value)
    {
        if (this.CloudGameContext == null)
            return;
        await CloudGameContext.GameLocalConfig.SaveConfigAsync(
            CloudGameLocalSettingName.EnableNetworkPanel,
            value.ToString(),
            this.CTS.Token
        );
        SeedUpdateQuality();
    }

    public void SeedUpdateQuality()
    {
       
        WeakReferenceMessenger.Default.Send<CloudQualityUpdateModel>(new()
        {
            Type =this.SelectQualitys==null? CloudQualityType.Clarity:this.SelectQualitys.Type,
            Fps = this.SelectFps,
            NetworkShow = this.ShowNetworkState,
            QaulityEnable = this.Enable
        });
    }
}

public class QualityWrapper
{
    public CloudQualityType Type { get; set; }

    public string DisplayName { get; set; }

    public static ObservableCollection<QualityWrapper> Create() =>
        [
            new QualityWrapper() { Type = CloudQualityType.Smooth, DisplayName = LanguageService.GetStringByText("流畅") },
            new QualityWrapper() { Type = CloudQualityType.Clarity, DisplayName = LanguageService.GetStringByText("原生") },
        ];
}
