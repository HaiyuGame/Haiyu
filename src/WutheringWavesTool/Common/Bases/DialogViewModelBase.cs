namespace Haiyu.Common.Bases;

public abstract partial class DialogViewModelBase : ViewModelBase
{
    private readonly DialogSession? _dialogSession;

    protected DialogViewModelBase(DialogSession dialogSession)
    {
        _dialogSession = dialogSession;
    }

    public object? Result { get; set; }

    [RelayCommand]
    protected Task Close()
    {
        return CloseAsync(Result ?? ContentDialogResult.None);
    }

    protected async Task CloseAsync(object? result = null)
    {
        Result = result;

        await BeforeCloseAsync();
        BeforeClose();

        if (_dialogSession is not null)
        {
            _dialogSession.Close(result);
        }
        else
        {
            Dispose();
        }

        AfterClose();
        await AfterCloseAsync();
    }

    public virtual void BeforeClose() { }

    public virtual void AfterClose() { }

    public virtual Task BeforeCloseAsync() => Task.CompletedTask;

    public virtual Task AfterCloseAsync() => Task.CompletedTask;
}
