using Haiyu.Models.Dialogs;
using Waves.Core.Models.Enums;

namespace Haiyu.Services;

public abstract class DialogManager : IDialogManager
{
    ContentDialog _dialog = null;
    public XamlRoot Root { get; private set; }

    public void RegisterRoot(XamlRoot root)
    {
        this.Root = root;
    }

    public void SetDialog(ContentDialog contentDialog)
    {
        this._dialog = contentDialog;
    }

    public async Task ShowLoginDialogAsync() => await ShowDialogAsync<LoginDialog>();

    public async Task ShowGameResourceDialogAsync(string contextName)
    {
        var dialog = Instance.Host.Services.GetRequiredService<GameResourceDialog>();
        dialog.SetData(contextName);
        dialog.XamlRoot = this.Root;
        this._dialog = dialog;
        await _dialog.ShowAsync();
    }


    public async Task ShowDialogAsync<T>()
        where T : ContentDialog, IDialog
    {
        if (_dialog != null)
            return;
        var dialog = Instance.Host.Services.GetRequiredService<T>();
        dialog.XamlRoot = this.Root;
        this._dialog = dialog;
        var result = await _dialog.ShowAsync();
        _dialog = null;
    }

    public async Task<ContentDialogResult> ShowDialogAsync<T>(object data)
        where T : ContentDialog, IDialog
    {
        if (_dialog != null)
        {
            _dialog.Hide();
            _dialog = null;
        }
        var dialog = Instance.Host.Services.GetRequiredService<T>();
        dialog.XamlRoot = this.Root;
        dialog.SetData(data);
        this._dialog = dialog;
        var result = await _dialog.ShowAsync();
        _dialog = null;
        return result;
    }

    public void CloseDialog()
    {
        if (_dialog == null)
            return;
        _dialog.Hide();
        GC.Collect();
    }

    public async Task<Result> GetDialogResultAsync<T, Result>(object? data)
        where T : ContentDialog, IResultDialog<Result>, new()
        where Result : new()
    {
        if (_dialog != null)
        {
            _dialog = null;
        }
        var dialog = Instance.Host.Services.GetRequiredService<T>();
        dialog.XamlRoot = this.Root;
        dialog.SetData(data);
        this._dialog = dialog;
        await _dialog.ShowAsync();
        var result = ((IResultDialog<Result>)_dialog).GetResult();
        _dialog = null;
        return result;
    }

    public async Task<SelectDownloadFolderResult> ShowSelectGameFolderAsync(Type type) =>
        await GetDialogResultAsync<SelectGameFolderDialog, SelectDownloadFolderResult>(type);

    /// <summary>
    /// 显示更新游戏对话框
    /// </summary>
    /// <param name="contextName">游戏核心</param>
    /// <param name="isShowUpdate">是否是更新游戏，否则是预下载</param>
    /// <returns></returns>
    public async Task<UpdateGameResult> ShowUpdateGameDialogAsync(string contextName, UpdateGameType type)=>
        await GetDialogResultAsync<UpdateGameDialog, UpdateGameResult>(new Tuple<string, UpdateGameType>(contextName, type));

    public async Task<SelectDownloadFolderResult> ShowSelectDownloadFolderAsync(Type type) =>
        await GetDialogResultAsync<SelectDownoadGameDialog, SelectDownloadFolderResult>(type);

    public async Task<CloseWindowResult> ShowCloseWindowResult() =>
        await GetDialogResultAsync<CloseDialog, CloseWindowResult>(null);

    public async Task ShowLocalUserManagerAsync()=>
        await ShowDialogAsync<LocalUserManagerDialog>();

    public async Task ShowGameEnhancedDialogAsync()=>
        await ShowDialogAsync<GameEnhancedDialog>("");
    public async Task<QRScanResult> GetQRLoginResultAsync() => await GetDialogResultAsync<QRLoginDialog, QRScanResult>(null);


    public async Task<ContentDialogResult> ShowMessageDialog(string header, string content, string closeText)
    {
        var dialog = new ContentDialog();
        dialog.XamlRoot = this.Root;
        dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
        dialog.PrimaryButtonText = content;
        dialog.CloseButtonText = closeText;
        dialog.IsSecondaryButtonEnabled = false;
        dialog.DefaultButton = ContentDialogButton.Close;
        dialog.Content = new TextBlock() { Text = header };
        var result = await dialog.ShowAsync();
        return result;
    }

    public async Task ShowWebGameDialogAsync() => await ShowDialogAsync<WebGameLogin>();

    public async Task ShowGameLauncherChacheDialogAsync(GameLauncherCacheArgs args) => await ShowDialogAsync<GameLauncherCacheManager>(args);

    public async Task<ContentDialogResult> ShowOKDialogAsync(string header, string content)
    {
        ContentDialog dialog = new ContentDialog();

        // XamlRoot must be set in the case of a ContentDialog running in a Desktop app
        dialog.XamlRoot = this.Root;
        dialog.Title = header;
        dialog.PrimaryButtonText = "确定";
        dialog.DefaultButton = ContentDialogButton.None;
        dialog.Content = content;

        var result = await dialog.ShowAsync();
        return result;
    }
}
