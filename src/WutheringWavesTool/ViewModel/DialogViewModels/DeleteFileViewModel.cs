using Waves.Core.Models.Enums;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class DeleteFileViewModel : DialogViewModelBase
{
    public DeleteFileViewModel(DialogSession dialogManager, IWindowManager windowManager)
        : base(dialogManager)
    {
        this.ContextName = null;
        _windowManager = windowManager;
    }

    public string? ContextName { get; private set; }

    private IGameContextV2 _gameContext;

    private bool bthEnable = true;
    private readonly IWindowManager _windowManager;

    [ObservableProperty]
    public partial double Current { get; set; }

    [ObservableProperty]
    public partial double MaxTotal { get; set; }

    [ObservableProperty]
    public partial bool IsProgressIndeterminate { get; set; } = true;

    public void SetDeleteFileArgs(string folder)
    {
        this.ContextName = folder;
        this._gameContext = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(
            ContextName
        );
    }

    bool GetCanInvoke() => bthEnable;

    [RelayCommand(CanExecute = nameof(GetCanInvoke))]
    public async Task CancelClose()
    {
        await this.Close();
    }

    [RelayCommand(CanExecute = nameof(GetCanInvoke))]
    public async Task DeleteResourceAsync()
    {
        var state = await this._gameContext.GetGameContextStatusAsync(this.CTS.Token);
        if (state.IsPredownloaded && state.PredownloaAcion)
        {
            await _windowManager.Shell.TipShow.ShowMessageAsync(
                LanguageService.GetStringByText("预下载期间禁止删除游戏"),
                Symbol.Clear
            );
            return;
        }
        if (this.ContextName is null)
        {
            await this.Close();
            return;
        }
        IsProgressIndeterminate = true;
        Current = 0;
        MaxTotal = 1;
        IProgress<(double deletedCount, double totalCount)> progress = new Progress<(
            double deletedCount,
            double count
        )>(
            (s) =>
            {
                IsProgressIndeterminate = s.count <= 0;
                Current = s.deletedCount;
                MaxTotal = Math.Max(1, s.count);
            }
        );
        this.bthEnable = false;
        this.CancelCloseCommand.NotifyCanExecuteChanged();
        this.DeleteResourceCommand.NotifyCanExecuteChanged();
        await _gameContext.DeleteResourceAsync(progress);
        await this.Close();
    }
}
