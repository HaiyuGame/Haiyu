using Haiyu.Models.Enums;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Waves.Core.GameContext.ContextsV2.Punish;
using Waves.Core.GameContext.ContextsV2.Waves;
using Waves.Core.Models.Enums;
using Waves.Core.Models.Options;
using Waves.Core.Services.GameResourceProvider;

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
                this.AppContext.WindowManager.Shell.Hide();
            }
        }
        if (_buttonAction == ButtonActionType.PrepareUpdate)
        {
            var localVersion = await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.LocalGameVersion
            );
            var result = await WindowManager.Shell.DialogManager.ShowUpdateGameDialogAsyncV2(
                this.GameContext.ContextName,
                UpdateGameType.UpdateGame, ResourceParameter
            );

            if (result == null)
                return;
            if (result.IsOk == false)
            {
                return;
            }
            _ =  Task.Run(async () => await GameContext.UpdateGameResourceAsync(result.DiffSavePath, result.Parameter));
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
                if (GameContext.GameResourceProvider is BundleGameResourceProvider provider && provider.SupportsPackageType)
                {
                    var parameter = await provider.ReadPredownloadParameterAsync(ResourceParameter, CTS.Token);
                    var plan = await GameContext.GetUpdateGameResourceAsync(parameter, CTS.Token);
                    if (plan.NeedsPackageTypeConfirmation)
                    {
                        var confirmation = await WindowManager.Shell.DialogManager.ShowMessageDialog(new ShowDialogOption
                        {
                            Context = LanguageService.GetStringByText("游戏侧资源量状态与启动器偏好不一致，可能在游戏内清理或补充过资源。按缓存资源量安装更新时，可能需要重新下载部分资源。"),
                            ShowPrimaryButton = true,
                            PrimaryText = LanguageService.GetStringByText("按缓存资源量继续安装"),
                            CloseText = LanguageService.GetStringByText("返回")
                        });
                        if (confirmation != ContentDialogResult.Primary)
                            return;
                    }
                }
                this.PauseIcon = "\uE769";
                _ = Task.Run(async () => await GameContext.StartInstallGameResource(InstallOption.CreateProdownlad(), ResourceParameter));
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
            await StartPreDownloadGame();
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
        if (!(GameContext.ContextName == nameof(WavesMainGameContextV2) || GameContext.ContextName == nameof(PunishMainGameContextV2)))
        {
            await WindowManager.Shell.DialogManager.ShowMessageDialog(new ShowDialogOption()
            {
                Context = LanguageService.GetStringByText("不支持的游戏类型")
            });
            return;
        }
        await WindowManager.Shell.DialogManager.ShowGameLocalTokenAsync(this.GameContext.ContextName);
    }
}
