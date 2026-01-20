using Haiyu.Services.DialogServices;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class LocalUserManagerViewModel : DialogViewModelBase
{
    public LocalUserManagerViewModel(
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager,
        IKuroAccountService kuroAccountService,
        IKuroClient kruoClient
    )
        : base(dialogManager)
    {
        KuroAccountService = kuroAccountService;
        KuroClient = kruoClient;
        RegisterMessager();
    }

    public IKuroClient KuroClient { get; }

    public IKuroAccountService KuroAccountService { get; }

    [ObservableProperty]
    public partial ObservableCollection<LocalAccount> Accounts { get; set; }

    private void RegisterMessager()
    {
        WeakReferenceMessenger.Default.Register<SetCurrentAccount>(this, SetCurrentAccountMethod);
        WeakReferenceMessenger.Default.Register<DeleteLocalAccount>(this, DeleteAccountMethod);
    }

    private void DeleteAccountMethod(object recipient, DeleteLocalAccount message)
    {
        throw new NotImplementedException();
    }

    private async void SetCurrentAccountMethod(object recipient, SetCurrentAccount message)
    {
        this.KuroAccountService.SetCurrentUser(message.userId,true);
        await RefreshAsync();
    }

    [RelayCommand]
    async Task Loaded()
    {
        await RefreshAsync();
    }

    async Task RefreshAsync()
    {
        Accounts = (await KuroAccountService.GetUsersAsync()).ToObservableCollection();
        foreach (var item in Accounts)
        {
            if (long.TryParse(item.TokenId, out var id))
            {
                var value = await KuroClient.GetWavesMineAsync(id, item.TokenDid,item.Token,this.CTS.Token);
                if (value == null)
                    continue;
                if(value.Success  == false)
                {
                    await KuroAccountService.DeleteUserAsync(item.TokenId);
                    continue;
                }
                item.Cover = value.Data.Mine.HeadUrl;
                item.DisplayName = value.Data.Mine.UserName;
                item.Phone = value.Data.Mine.Mobile;
            }
        }
    }
}
