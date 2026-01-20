using Haiyu.Models.Dialogs;

namespace Haiyu.Services.Contracts;

public interface IDialogManager
{
    public XamlRoot Root { get; }
    public void SetDialog(ContentDialog contentDialog);
    public void RegisterRoot(XamlRoot root);
    public Task ShowLoginDialogAsync();
    public Task ShowGameResourceDialogAsync(string contextName);
    public Task<Result> GetDialogResultAsync<T, Result>(object? data)
        where T : ContentDialog, IResultDialog<Result>, new()
        where Result : new();
    public Task ShowLocalUserManagerAsync();
    public Task<SelectDownloadFolderResult> ShowSelectGameFolderAsync(Type type);
    public Task<SelectDownloadFolderResult> ShowSelectDownloadFolderAsync(Type type);
    public Task<CloseWindowResult> ShowCloseWindowResult();
    public Task<QRScanResult> GetQRLoginResultAsync();
    public Task<UpdateGameResult> ShowUpateGameDialogAsync(string contextName);

    public void CloseDialog();

    public Task<ContentDialogResult> ShowMessageDialog(string header, string content, string closeText);
    Task ShowWebGameDialogAsync();

    Task ShowGameLauncherChacheDialogAsync(GameLauncherCacheArgs args);

    Task<ContentDialogResult> ShowOKDialogAsync(string header, string content);

    Task ShowGameEnhancedDialogAsync();
}
