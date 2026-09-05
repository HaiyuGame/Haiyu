using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Models.Enums;

namespace Haiyu.ViewModel.GameViewModels;

partial class KuroGameContextViewModelV2
{
    /// <summary>
    /// 预下载卡片可见性
    /// </summary>
    [ObservableProperty]
    public partial Visibility PredCardVisibility { get; set; } = Visibility.Collapsed;


    [ObservableProperty]
    public partial Visibility PredDownloadBthVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility PredDownloadingVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility PredDownloadDoneVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility PreAdvanceVisiblity { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial double PreDownloadProgress { get; set; } = 0;

    [ObservableProperty]
    public partial string PreDownloadIcon { get; set; } = "\uEBD3";



    /// <summary>
    /// 预下载进度
    /// </summary>
    [ObservableProperty]
    public partial double PreProgress { get; set; }

    /// <summary>
    /// 预下载执行步骤
    /// </summary>
    [ObservableProperty]
    public partial string PreSetupText { get; set; }

    [ObservableProperty]
    public partial string PreSetupHeaderText { get; set; }

    [ObservableProperty]
    public partial string PreSpeedText { get; set; }

    [ObservableProperty]
    public partial string PreDownloadSizeText { get; set; }


    [RelayCommand]
    async Task StartPreDownloadGame()
    {
        if(GameContext.ProdDownloadState != null &&( GameContext.ProdDownloadState.IsActive || !GameContext.ProdDownloadState.CancelToken.IsCancellationRequested))
        {
            if (this.GameContext.ProdDownloadState.IsPaused)
            {
                this.PreDownloadIcon = "\uE769";
                await this.GameContext.ResumeDownloadAsync();
                return;
            }
            else
            {
                this.PreDownloadIcon = "\uE769";
                await this.GameContext.PauseDownloadAsync();
                return;
            }
        }
        var result = await WindowManager.Shell.DialogManager.ShowUpdateGameDialogAsync(
            this.GameContext.ContextName,
            UpdateGameType.ProDownload
        );
        if (result.IsOk)
        {
            StartBackground(() => this.GameContext.StartProdDownloadGameResourceAsync());
        }
    }


    [RelayCommand]
    async Task StopDownloadGame()
    {
        await this.GameContext.StopCannelTaskAsync();
    }

    [RelayCommand]
    async Task RepreCheck()
    {
        var done =  await this.GameContext.GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadFolderDone);
        var version =  await this.GameContext.GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadVersion);
        var path =  await this.GameContext.GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadPath);
        var launcher = await this.GameContext.GetGameLauncherSourceAsync(null,this.CTS.Token);
        if(string.IsNullOrWhiteSpace(version))
        {
            done = "false";
        }
        if (done!= null && done.ToLower() == "true" && Directory.Exists(path))
        {
            StartBackground(() => this.GameContext.StartProdDownloadGameResourceAsync());
        }
        else
        {
            StartBackground(()=>StartPreDownloadGame());
        }
    }

    /// <summary>
    /// 提前安装
    /// </summary>
    private void ShowAdvanceInstallBth(GameContextStatus status)
    {
        PreAdvanceVisiblity = Visibility.Visible;
    }

    [RelayCommand]
    public async Task AdvanceGameAsync()
    {
        var result =  await WindowManager.Shell.DialogManager.ShowMessageDialog(new ShowDialogOption()
        {
            CloseText = LanguageService.GetStringByText("取消"),
            Context = LanguageService.GetStringByText("提前安装游戏文件之后，在最新版本开服之前无法正常启动游戏，请确认是否安装"),
            ShowPrimaryButton = true,
            PrimaryText = LanguageService.GetStringByText("确定安装")
        });
        if(result == ContentDialogResult.None)
        {
            return;
        }
        this.StartBackground(this.GameContext.AdvanceInstallGameResourceAsync);
    }
}
