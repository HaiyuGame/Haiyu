using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace Haiyu.ViewModel.GameViewModels;

partial class KuroGameContextViewModel
{
    [RelayCommand]
    async Task UpdateGameAsync()
    {
        if (_bthType == 3)
        {
            if (await GameContext.StartGameAsync())
            {
                this.AppContext.MinToTaskbar();
            }
        }
        if (_bthType == 4)
        {
            var localVersion = await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.LocalGameVersion
            );
            var result = await DialogManager.ShowUpdateGameDialogAsync(
                this.GameContext.ContextName,
                Models.Enums.UpdateGameType.UpdateGame
            );

            if (result == null)
                return;
            if (result.IsOk == false)
            {
                return;
            }
            await GameContext.UpdataGameAsync(result.DiffSavePath);
        }
        if (_bthType == 6)
        {
            var diffDone = await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.ProdDownloadFolderDone
            );
            var diffPath = await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.ProdDownloadPath
            );
            if(bool.TryParse(diffDone,out var done) && done && !string.IsNullOrWhiteSpace(diffPath))
            {
                await GameContext.StartInstallPredGame(diffPath);
            }
            else
            {
                //跳转会更新游戏
                _bthType = 4;
                await UpdateGameAsync();
            }
        }
    }
}
