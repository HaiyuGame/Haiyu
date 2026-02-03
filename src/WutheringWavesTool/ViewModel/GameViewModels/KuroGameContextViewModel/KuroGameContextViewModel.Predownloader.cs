using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.ViewModel.GameViewModels;

partial class KuroGameContextViewModel
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
    public partial double PreDownloadProgress { get; set; } = 0;

    [ObservableProperty]
    public partial string PreDownloadIcon { get; set; }
    

    [RelayCommand]
    async Task StartPreDownloadGame()
    {
        var result = await DialogManager.ShowUpdateGameDialogAsync(this.GameContext.ContextName, Models.Enums.UpdateGameType.ProDownload);
        if (result.IsOk)
        {
            await this.GameContext.StartDownloadProdGame(result.DiffSavePath);
        }
    }

    [RelayCommand]
    async Task PausePreDownloadGame()
    {
        var status = await this.GameContext.GetGameContextStatusAsync(this.CTS.Token);
        if (!status.IsPause)
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
        await this.GameContext.StopDownloadAsync();
        var status = await this.GameContext.GetGameContextStatusAsync(this.CTS.Token);
        if (status.IsPredownloaded)
        {
            PredCardVisibility = Visibility.Visible;
            if (!status.PredownloadedDone)
            {
                PredDownloadBthVisibility = Visibility.Visible;
                this.PredDownloadingVisibility = Visibility.Collapsed;
            }
        }
    }

    private async Task GameContext_GameContextProdOutput(object sender, GameContextOutputArgs args)
    {
        await AppContext.TryInvokeAsync(async () =>
        {
            if (args.Type == Waves.Core.Models.Enums.GameContextActionType.Download || args.Type == Waves.Core.Models.Enums.GameContextActionType.Verify)
            {
                this.PredDownloadingVisibility = Visibility.Visible;
                this.PredDownloadBthVisibility = Visibility.Collapsed;
                this.PreDownloadProgress = args.ProgressPercentage;
                if(args.IsAction == true && args.IsPause == true)
                {
                    PreDownloadIcon = "\uE768";
                }
                else
                {
                    PreDownloadIcon = "\uE769";
                }
            }
            else if (args.Type == Waves.Core.Models.Enums.GameContextActionType.None)
            {
                await this.RefreshCoreAsync();
            }
        });
    }
}
