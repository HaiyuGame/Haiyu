using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Haiyu.KuroClient;
using Haiyu.Mobile.Common;
using Haiyu.Mobile.Contracts;
using Haiyu.Mobile.Services;
using Haiyu.Mobile.ViewModels.ItemsViewModles;
using Plugin.Maui.BottomSheet.Navigation;
using Waves.Api.Models.Communitys;

namespace Haiyu.Mobile.ViewModels.ButtonSheet;

public partial class AccountMoreSessionViewModel : ButtonSheetViewModel
{
    public AccountMoreSessionViewModel(
        IKuroClient kuroClient,
        IMobileLocalAccountService mobileLocalAccountService,
        ItemFactory itemFactory
    )
    {
        KuroClient = kuroClient;
        MobileLocalAccountService = mobileLocalAccountService;
        ItemFactory = itemFactory;
    }

    public string UserId { get; private set; }
    public IKuroClient KuroClient { get; }
    public IMobileLocalAccountService MobileLocalAccountService { get; }
    public ItemFactory ItemFactory { get; }
    public KuroAccount RoleItem { get; private set; }

    [ObservableProperty]
    public partial ObservableCollection<GameRoilItemViewModel> WavesRoils { get; set; }


    [ObservableProperty]
    public partial ObservableCollection<GameRoilItemViewModel> PunishRoils { get; set; }

    public override void OnNavigatedTo(IBottomSheetNavigationParameters parameters)
    {
        if (parameters.TryGetValue("userId", out var value) && value is string userId)
        {
            this.UserId = userId;
        }
    }

    [RelayCommand]
    async Task Loaded()
    {
        var localUser = await this.MobileLocalAccountService.GetUserAsync(this.UserId);
        if (localUser == null)
        {
            await Toast.Make("账号失效", ToastDuration.Short, 14).Show();
            return;
        }
        this.RoleItem = KuroAccount.Create(localUser);
        await RefreshDataAsync();
    }

    [RelayCommand]
    async Task RefreshDataAsync()
    {
        var waves = await this.KuroClient.GetGamerAsync(
            this.RoleItem,
            3,
            this.CTS.Token
        );
        var punish = await this.KuroClient.GetGamerAsync(
            this.RoleItem,
            2,
            this.CTS.Token
        );
        if(waves != null && waves.Success)
        {
            this.WavesRoils = ItemFactory.CreateGameRoilItems(waves.Data);
        }
        else
        {
            await Toast.Make("鸣潮游戏角色获取失败",ToastDuration.Short,14).Show();
        }
        if (punish != null && punish.Success)
        {
            this.WavesRoils = ItemFactory.CreateGameRoilItems(punish.Data);
        }
        else
        {
            await Toast.Make("战双游戏角色获取失败", ToastDuration.Short, 14).Show();
        }
    }
}
