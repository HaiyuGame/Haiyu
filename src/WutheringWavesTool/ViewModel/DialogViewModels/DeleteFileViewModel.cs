using Haiyu.Services.DialogServices;
using Waves.Core.Models.Enums;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class DeleteFileViewModel : DialogViewModelBase
{
    public DeleteFileViewModel(
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager,
        ITipShow tipShow
    )
        : base(dialogManager)
    {
        this.ContextName = null;
        this.TipShow = tipShow;
    }

    public ITipShow TipShow { get; }

    public string? ContextName { get; private set; }

    private IGameContextV2 _gameContext;

    private bool bthEnable = true;

    [ObservableProperty]
    public partial double Current { get; set; }

    [ObservableProperty]
    public partial double MaxTotal { get; set; }

    public void SetDeleteFileArgs(string folder)
    {
        this.ContextName = folder;
        this._gameContext = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(
            ContextName
        );
    }

    bool GetCanInvoke() => bthEnable;

    [RelayCommand(CanExecute =nameof(GetCanInvoke))]
    public async Task CancelClose()
    {
        this.Close();
    }

    [RelayCommand(CanExecute = nameof(GetCanInvoke))]
    public async Task DeleteResourceAsync() 
    {
        var state = await this._gameContext.GetGameContextStatusAsync(this.CTS.Token);
        if (state.IsPredownloaded && state.PredownloaAcion)
        {
            await TipShow.ShowMessageAsync("预下载期间禁止修复游戏！", Symbol.Clear);
            return;
        }
        if (this.ContextName is null)
        {
            this.Close();
            return;
        }
        IProgress < (double deletedCount, double totalCount) > progress = new Progress<(double deletedCount, double count)>((s) =>
        {
            Current = s.deletedCount;
            MaxTotal = s.count;
        });
        this.bthEnable = false; 
        this.CancelCloseCommand.NotifyCanExecuteChanged();
        this.DeleteResourceCommand.NotifyCanExecuteChanged();
        await _gameContext.DeleteResourceAsync(progress);
        this.Close();
    }
}
