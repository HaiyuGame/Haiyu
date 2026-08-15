using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Haiyu.KuroClient;
using Haiyu.Mobile.Common;
using Haiyu.Mobile.Contracts;
using Haiyu.Mobile.Models.Messanger;
using Haiyu.Mobile.Services;
using Haiyu.Mobile.ViewModels.ItemsViewModles;
using Haiyu.Mobile.ViewModels.Popups;
using Haiyu.Mobile.Views;
using Haiyu.Mobile.Views.ButtonSheets;
using Microsoft.Maui.Controls.Shapes;
using Plugin.BottomSheet;
using Plugin.Maui.BottomSheet.Navigation;

namespace Haiyu.Mobile.ViewModels;

public sealed partial class UserManagerViewModel : ViewModelBase
{
    private readonly ItemFactory _itemFactory;
    private readonly IBottomSheetNavigationService _bottomSheetNavigationService;
    private readonly IPopupService _popupService;

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }
    public IMobileLocalAccountService MobileLocalAccountService { get; }

    public UserManagerViewModel(
        IMobileLocalAccountService mobileLocalAccountService,
        ItemFactory itemFactory,
        IBottomSheetNavigationService bottomSheetNavigationService,IPopupService popupService
    )
    {
        MobileLocalAccountService = mobileLocalAccountService;
        _itemFactory = itemFactory;
        _bottomSheetNavigationService = bottomSheetNavigationService;
        _popupService = popupService;
        RegisterMessanger();
    }

    private void RegisterMessanger()
    {
        this.Messenger.Register<LocalUserSessionShowMessanger>(this, LocalUserSessionShowMethod);
        this.Messenger.Register<HomeAccountMessanger>(this, HomeAccountMethod);
    }

    private async void HomeAccountMethod(object recipie, HomeAccountMessanger messager)
    {
        if (messager.isRefresh)
            await this.Loaded();
    }

    private async void LocalUserSessionShowMethod(
        object recipient,
        LocalUserSessionShowMessanger messanger
    )
    {
        await _bottomSheetNavigationService.NavigateToAsync(
            "accountMore",
            new BottomSheetNavigationParameters() { { "userId", messanger.vm.BaseData.TokenId } },
            (s) =>
            {
                s.States = [BottomSheetState.Peek];
                s.CurrentState = BottomSheetState.Peek;
                s.IsModal = true;
                s.SizeMode = BottomSheetSizeMode.FitToContent;
                s.IsCancelable = true;
                s.IsDraggable = false;
                s.HasHandle = true;
            }
        );
    }

    [ObservableProperty]
    public partial ObservableCollection<LocalUserItemViewModel> LocalUsers { get; set; }

    [RelayCommand]
    async Task Loaded()
    {
        IsRefreshing = true;
        var users = await MobileLocalAccountService.GetUsersAsync();
        if (users != null)
        {
            LocalUsers = _itemFactory.CreateLocalUserItems(users);
        }
        IsRefreshing = false;
    }



    [RelayCommand]
    async Task CreateKuroUser()
    {
        await Shell.Current.GoToAsync(nameof(AddKuroUserPage), true);
    }

    [RelayCommand]
    async Task CreateTokenKuroUser()
    {
        var page = Shell.Current?.CurrentPage ?? Shell.Current;
        var result = await _popupService.ShowPopupAsync<AddKuroTokenViewModel>(
            page,
            new PopupOptions()
            {
                CanBeDismissedByTappingOutsideOfPopup = true,
                Shape = new RoundRectangle
                {
                    CornerRadius = new CornerRadius(16)
                },
                Shadow = null,
                
            },
            this.CTS.Token
        );
        if (result.WasDismissedByTappingOutsideOfPopup)
        {
            return;
        }
        await this.Loaded();
    }
}
