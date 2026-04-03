using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Waves.Core.Models.Enums;

namespace Haiyu.ViewModel.GameViewModels;

partial class KuroGameContextViewModelV2
{
    [RelayCommand]
    async Task UpdateGameAsync()
    {
        if (_buttonAction == ButtonActionType.StartGame)
        {
            if (await GameContext.StartGameAsync())
            {
                this.AppContext.MinToTaskbar();
            }
        }
        if (_buttonAction == ButtonActionType.PrepareUpdate)
        {
            Task.Run(async () => await GameContext.UpdateGameResourceAsync());
        }
        if (_buttonAction == ButtonActionType.InstallPreDownload)
        {
            var diffDone = await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.ProdDownloadFolderDone
            );
            var diffPath = await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.ProdDownloadPath
            );
            if(bool.TryParse(diffDone,out var done) && done)
            {
                this.PauseIcon = "\uE769";
                Task.Run(async () => await GameContext.StartInstallGameResource(true));
            }
            else
            {
                _buttonAction = ButtonActionType.PrepareUpdate;
                Task.Run(async()=> await UpdateGameAsync());
            }
        }
    }

    [RelayCommand]
    async Task StartDownloadProdGameResource()
    {
        var status = await this.GameContext.GetGameContextStatusAsync(this.CTS.Token);
        if (status == null)
            return;
        if(GameContext.DownloadState== null)
        {
            this.PreDownloadIcon = "\uEBD3";
            await this.GameContext.StartProdDownloadGameResourceAsync();
            return;
        }
        if (status.IsPause || GameContext.DownloadState.IsPaused)
        {
            await this.GameContext.ResumeDownloadAsync();
            this.PreDownloadIcon = "\uE768";
        }
        else
        {
            await this.GameContext.PauseDownloadAsync();
            this.PreDownloadIcon = "\uE768";
        }
    }
}
