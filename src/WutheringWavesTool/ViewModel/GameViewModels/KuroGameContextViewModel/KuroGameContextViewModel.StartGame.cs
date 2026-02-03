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
            if( await GameContext.StartGameAsync())
            {
                this.AppContext.MinToTaskbar();
                
            }
        }
        if (_bthType == 4)
        {
            var localVersion = await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.LocalGameVersion
            );
            var result = await DialogManager.ShowUpdateGameDialogAsync(this.GameContext.ContextName, Models.Enums.UpdateGameType.UpdateGame);
            
            if (result == null)
                return; 
            if (result.IsOk == false)
            {
                return;
            }
            await GameContext.UpdataGameAsync(result.DiffSavePath);
        }
    }
}
