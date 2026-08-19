using Haiyu.Services.DialogServices;
using Waves.Core.Services;

namespace Haiyu.Common.Bases;

public abstract partial class DialogViewModelBase : ViewModelBase
{
    public DialogViewModelBase(
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager
    )
    {
        DialogManager = dialogManager;
        this.Logger = Instance.Host.Services.GetKeyedService<LoggerService>("AppLog");
    }

    public DialogViewModelBase()
    {
        DialogManager = Instance.Host.Services.GetRequiredKeyedService<IDialogManager>(nameof(MainDialogService));
        this.Logger = Instance.Host.Services.GetKeyedService<LoggerService>("AppLog");
    }

    public ContentDialogResult? Result { get; set; }
    public IDialogManager DialogManager { get; }
    public LoggerService Logger { get; }

    [RelayCommand]
    protected async Task Close()
    {
        if (Result == null)
            this.Result = ContentDialogResult.None;
        await BeforeCloseAsync();
        BeforeClose();
        DialogManager.CloseDialog();
        Dispose();
        AfterClose();
        await AfterCloseAsync();
    }

    public virtual void BeforeClose() { }

    public virtual void AfterClose() { }

    public async virtual Task BeforeCloseAsync() { }
    public async virtual Task AfterCloseAsync() { }
}
