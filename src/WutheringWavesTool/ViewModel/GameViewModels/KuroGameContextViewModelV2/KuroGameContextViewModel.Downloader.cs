namespace Haiyu.ViewModel.GameViewModels;

partial class KuroGameContextViewModelV2
{
    [ObservableProperty]
    public partial double MaxProgressValue { get; set; }

    [ObservableProperty]
    public partial double CurrentProgressValue { get; set; }

    [ObservableProperty]
    public partial int DownloadSpeedValue { get; set; }


    #region 通知

    #endregion

    [RelayCommand]
    async Task PauseDownloadTask()
    {
        var status = await this.GameContext.GetGameContextStatusAsync(this.CTS.Token);
        if (status.IsPause)
        {
            if (await this.GameContext.ResumeDownloadAsync())
            {
                this.BottomBarContent = "下载已恢复";
                this.PauseIcon = "\uE769";
            }
        }
        else
        {
            if (await this.GameContext.PauseDownloadAsync())
            {
                this.BottomBarContent = "下载已经暂停";
                this.PauseIcon = "\uE768";
            }
        }
    }

    [RelayCommand]
    async Task CancelDownloadTask()
    {
        Logger.WriteInfo($"取消当前操作");
        await GameContext.StopCannelTaskAsync();
        var status = await GameContext.GetGameContextStatusAsync();
        if (!status.IsLauncher)
        {
            await this.GameContext.GameLocalConfig.SaveConfigAsync(
                GameLocalSettingName.GameLauncherBassFolder,
                ""
            );
            await this.GameContext.GameLocalConfig.SaveConfigAsync(
                GameLocalSettingName.GameLauncherBassProgram,
                ""
            );
            await this.GameContext.GameLocalConfig.SaveConfigAsync(
                GameLocalSettingName.LocalGameUpdateing,
                "False"
            );
        }
        this.ProgressState_OnProgressChanged(this.GameContext.ProgressState);
        
    }

    [RelayCommand]
    async Task SetDownloadSpeedAsync()
    {
        Logger.WriteInfo($"设置下载限速");
        await GameContext.SetDownloadSpeedAsync(DownloadSpeedValue * 1024 * 1024);
    }

}
