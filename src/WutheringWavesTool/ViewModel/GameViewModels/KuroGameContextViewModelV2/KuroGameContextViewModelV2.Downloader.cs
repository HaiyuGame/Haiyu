namespace Haiyu.ViewModel.GameViewModels;

partial class KuroGameContextViewModelV2
{
    
    #region 进度显示

    [ObservableProperty]
    public partial ObservableCollection<DownloadActiveFileItem> ActiveFilesItems { get; set; } = new();

    [ObservableProperty]
    public partial string CurrentStepText { get; set; }

    [ObservableProperty]
    public partial int MaxStep { get; set; }

    [ObservableProperty]
    public partial int CurrentStep { get; set; }

    [ObservableProperty]
    public partial string SpeedText { get; set; }

    [ObservableProperty]
    public partial string ActiveFile { get; set; }

    [ObservableProperty]
    public partial double MaxProgressValue { get; set; }

    [ObservableProperty]
    public partial double CurrentProgressValue { get; set; }

    [ObservableProperty]
    public partial int DownloadSpeedValue { get; set; }

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial string CurrentByteText { get; set; }
    [ObservableProperty]
    public partial string MaxByteText { get; set; }

    [ObservableProperty]
    public partial string CurrentFile { get; set; }
    [ObservableProperty]
    public partial string FileTotal { get; set; }
    #endregion

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
        await GameContext.SetDownloadSpeedAsync(DownloadSpeedValue);
    }

}
