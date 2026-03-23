using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Waves.Core.Models.Enums;

namespace Haiyu.ViewModel.GameViewModels;

partial class KuroGameContextViewModel
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
            var localVersion = await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.LocalGameVersion
            );
            var result = await DialogManager.ShowUpdateGameDialogAsync(
                this.GameContext.ContextName,
                UpdateGameType.UpdateGame
            );

            if (result == null)
                return;
            if (result.IsOk == false)
            {
                return;
            }
            this.PauseIcon = "\uE769";
            Task.Run(async () => await GameContext.UpdataGameAsync(result.DiffSavePath));
        }
        if (_buttonAction == ButtonActionType.InstallPreDownload)
        {
            var diffDone = await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.ProdDownloadFolderDone
            );
            var diffPath = await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.ProdDownloadPath
            );
            if(bool.TryParse(diffDone,out var done) && done && !string.IsNullOrWhiteSpace(diffPath))
            {
                this.PauseIcon = "\uE769";
                Task.Run(async () => await GameContext.StartInstallPredGame(diffPath));
            }
            else
            {
                //跳转会更新游戏
                _buttonAction = ButtonActionType.PrepareUpdate;
                Task.Run(async()=> await UpdateGameAsync());
            }
        }
    }
}
