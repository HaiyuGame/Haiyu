using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoogleGson;
using Haiyu.KuroClient;
using Haiyu.Mobile.Common;
using Haiyu.Mobile.Contracts;
using Waves.Api.Models.Messanger;

namespace Haiyu.Mobile.ViewModels.Popups;

public sealed partial class AddKuroTokenViewModel : ViewModelBase
{
    public AddKuroTokenViewModel(
        IKuroClient kuroClient,
        IMobileLocalAccountService mobileLocalAccountService,
        IPopupService popupService
    )
    {
        KuroClient = kuroClient;
        MobileLocalAccountService = mobileLocalAccountService;
        PopupService = popupService;
    }

    [ObservableProperty]
    public partial string Token { get; set; }

    [ObservableProperty]
    public partial string Did { get; set; }

    [ObservableProperty]
    public partial string PlayerId { get; set; }
    public IKuroClient KuroClient { get; }
    public IMobileLocalAccountService MobileLocalAccountService { get; }
    public IPopupService PopupService { get; }

    [RelayCommand]
    async Task Login()
    {
        if (long.TryParse(PlayerId, out var _tokenID))
        {
            var requestAccount = new KuroAccount
            {
                UserId = PlayerId,
                Token = Token,
                DeviceId = Did,
            };
            var mine = await KuroClient.GetWavesMineAsync(requestAccount, _tokenID, this.CTS.Token);
            if (mine != null && mine.Code == 200)
            {
                LocalAccount account = new LocalAccount();
                account.Token = Token;
                account.TokenId = PlayerId;
                account.TokenDid = Did;
                await MobileLocalAccountService.SaveUserAsync(account);
                await PopupService.ClosePopupAsync(Shell.Current?.CurrentPage!, true);
            }
            else
            {
                await PopupService.ClosePopupAsync(Shell.Current?.CurrentPage!, false);
            }
        }
    }
}
