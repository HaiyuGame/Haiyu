using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Haiyu.Mobile.Common;
using Haiyu.Mobile.ViewModels.Popups;

namespace Haiyu.Mobile.ViewModels;

public sealed partial class AddKuroUserViewModel : ViewModelBase
{
    private readonly IServiceProvider _service;
    private readonly IPopupService _popupService;

    public AddKuroUserViewModel(IServiceProvider service, IPopupService popupService)
    {
        _service = service;
        _popupService = popupService;
    }

    [ObservableProperty]
    public partial string UserPhone { get; set; }

    [ObservableProperty]
    public partial string VerifyCode { get; set; }

    [RelayCommand]
    public async Task SignGeetAsync()
    {
        var page = Shell.Current?.CurrentPage ?? Shell.Current;
        if (page is null)
            return;

        var result = await _popupService.ShowPopupAsync<LoginGeetViewModel, string>(
            page,
            new PopupOptions()
            {
                CanBeDismissedByTappingOutsideOfPopup = false,
                Shape = null,
                Shadow = null
            },
            this.CTS.Token
        );

        if (result.WasDismissedByTappingOutsideOfPopup || string.IsNullOrWhiteSpace(result.Result))
            return;

        // result.Result 为极验回调 JSON
        System.Diagnostics.Debug.WriteLine($"Geet result: {result.Result}");
    }
}
