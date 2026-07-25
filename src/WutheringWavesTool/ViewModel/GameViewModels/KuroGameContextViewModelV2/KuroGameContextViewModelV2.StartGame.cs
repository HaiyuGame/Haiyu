using Haiyu.Models.Enums;
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
            if ((await GameContext.StartGameAsync()))
            {
                this.WallpaperService.PauseVideo();
            }
            if((await AppSettings.GetStartGameAllowCloseMainAsync()) == true)
            {
                this.AppContext.MinToTaskbar();
            }
        }
        if (_buttonAction == ButtonActionType.PrepareUpdate)
        {
            var localVersion = await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.LocalGameVersion
            );
            var result = await DialogManager.ShowUpdateGameDialogAsyncV2(
                this.GameContext.ContextName,
                UpdateGameType.UpdateGame
            );

            if (result == null)
                return;
            if (result.IsOk == false)
            {
                return;
            }
            _ =  Task.Run(async () => await GameContext.UpdateGameResourceAsync());
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
                _ = Task.Run(async () => await GameContext.StartInstallGameResource(InstallOption.CreateProdownlad()));
            }
            else
            {
                _buttonAction = ButtonActionType.PrepareUpdate;
                _ = Task.Run(async()=> await UpdateGameAsync());
            }
        }
    }

    [RelayCommand]
    async Task StartDownloadProdGameResource()
    {
        var status = await this.GameContext.GetGameContextStatusAsync(this.CTS.Token);
        if (status == null)
            return;
        if(GameContext.ProdDownloadState== null)
        {
            this.PreDownloadIcon = "\uEBD3";
            StartBackground(()=> this.GameContext.StartProdDownloadGameResourceAsync());
            return;
        }
        if (status.IsPause || GameContext.ProdDownloadState.IsPaused)
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

    [RelayCommand]
    async Task ShowGameLocalTokenWindow()
    {
        var window = this.ViewFactory.ShowGameLocalTokenWindowAsync(this.GameContext.ContextName);
        window.AppWindow.Show();
    }
}
