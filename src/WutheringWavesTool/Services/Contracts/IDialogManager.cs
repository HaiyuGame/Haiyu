using Waves.Core.Models.Options;
using Haiyu.Common.Contracts;
using Haiyu.Models.Dialogs;
using Haiyu.Plugin.Models;
using Waves.Api.Models.CloudGame;
using Waves.Core.Models.CloudGame;
using Waves.Core.Models.Enums;

namespace Haiyu.Services.Contracts;

public interface IDialogManager
{
    public XamlRoot Root { get; }
    public void SetDialog(ContentDialog contentDialog);
    public void RegisterRoot(XamlRoot root);
    public Task ShowLoginDialogAsync();
    public Task<Result> GetDialogResultAsync<T, Result>(object? data)
        where T : ContentDialog, IDialog;
    public Task ShowLocalUserManagerAsync();
    public Task ShowUpdateDialog(DisplayVersionInfo info);
    public Task<SelectDownloadFolderResult> ShowSelectGameFolderV2Async(Type type, GameResourceParameter? parameter = null);
    public Task<SelectDownloadFolderResult?> ShowSelectDownloadFolderV2Async(Type type, GameResourceParameter? parameter = null);
    public Task<CloseWindowResult?> ShowCloseWindowResult();
    public Task<QRScanResult> GetQRLoginResultAsync();
    public Task<UpdateGameResult?> ShowUpdateGameDialogAsyncV2(
        string contextName,
        UpdateGameType type,
        GameResourceParameter? parameter = null
    );
    public Task<LauncheNodeConfig?> ShowSelectGameNodeAsync(string id);
    public Task ShowWavesCloudSettingAsync(GameType ype);

    Task ShowCloudUserManagerDialogAsync();
    Task ShowGameResourceV2DialogAsync(string contextName);
    public Task ShowDeleteGameResource(string contentName);
    public void CloseDialog();

    public Task<ContentDialogResult> ShowMessageDialog(ShowDialogOption option);
    Task ShowWebGameDialogAsync();

    Task ShowGameLauncherChacheDialogAsync(GameLauncherCacheArgs args);

    Task ShowWebViewCabManangerAsync();
    Task<ContentDialogResult> ShowOKDialogAsync(string header, string content);
    Task ShowGameSettingAsync(string contextName);
    Task ShowGameEnhancedDialogAsync();

    Task ShowGameLocalTokenAsync(string contextName);

    Task ShowClearMemoryAsync();

    Task<BunleResourceSize?> ShowBunleGameDialogAsync(string contextName);
}
