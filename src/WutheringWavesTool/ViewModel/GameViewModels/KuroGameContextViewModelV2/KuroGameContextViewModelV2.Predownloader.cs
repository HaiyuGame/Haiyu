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
    public partial double PreDownloadProgress { get; set; } = 0;

    [ObservableProperty]
    public partial string PreDownloadIcon { get; set; }

    [RelayCommand]
    async Task StartPreDownloadGame()
    {
        var result = await DialogManager.ShowUpdateGameDialogAsync(
            this.GameContext.ContextName,
            UpdateGameType.ProDownload
        );
        if (result.IsOk)
        {
            await this.GameContext.StartProdDownloadGameResourceAsync();
        }
    }

    [RelayCommand]
    async Task PausePreDownloadGame()
    {
        var status = await this.GameContext.GetGameContextStatusAsync(this.CTS.Token);
        if (!status.IsProdownPause)
        {
            await this.GameContext.PauseDownloadAsync();
            this.PreDownloadIcon = "\uE768";
        }
        else
        {
            await this.GameContext.ResumeDownloadAsync();
            this.PreDownloadIcon = "\uE769";
        }
    }

    [RelayCommand]
    async Task StopDownloadGame()
    {
        await this.GameContext.StopCannelTaskAsync();
        //await this.GameContext_GameContextProdOutput(
        //        this.GameContext,
        //        new GameContextOutputArgs()
        //        {
        //            Type = GameContextActionType.None,
        //            CurrentSize = 0,
        //            TotalSize = 0,
        //            DownloadSpeed = 0,
        //            VerifySpeed = 0,
        //            RemainingTime = TimeSpan.Zero,
        //        }
        //    )
        //    .ConfigureAwait(false);
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
            await this.GameContext.StartProdDownloadGameResourceAsync();
        }
        else
        {
            await StartPreDownloadGame();
        }
    }

}
