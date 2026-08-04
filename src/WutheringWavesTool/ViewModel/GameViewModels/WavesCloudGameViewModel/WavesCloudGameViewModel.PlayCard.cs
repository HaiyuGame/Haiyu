using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Common;
using Waves.Core.Models.CloudGame;

namespace Haiyu.ViewModel.GameViewModels;

partial class WavesCloudGameViewModel
{
    async Task RefreshUserAsync()
    {
        var users = KuroCloudGameContext.WavesCloudSurivivalService.Cache.ToList();
        await App.TryInvokeAsync(async () =>
        {
            this.Logins = new(users);
            if (Logins.Count > 0)
            {
                SelectLogin = Logins[0];
            }
        });
    }

    [RelayCommand]
    async Task RefreshCardAsync()
    {
        IsRefreshing = true;
        if (this.KuroCloudGameContext == null)
        {
            await TipShow.ShowMessageAsync(LanguageService.GetStringByText("游戏核心为空！请尝试刷新页面"),Symbol.Clear);
            return;
        }
        await this.KuroCloudGameContext.WavesCloudSurivivalService.RefreshTaskAsync();
        await this.RefreshUserAsync();
        await this.RefreshCloudNodesAsync();
        IsRefreshing = false;
    }

    private async Task RefreshCloudNodesAsync()
    {
        var nodes =
            await KuroCloudGameContext.WavesCloudSurivivalService.WavesCloudGameService.CloudNetworkSpeedTestService.GetNodeListAsync(
                CloudNetworkSpeedTestService.DefaultBaseUrl,
                this.CTS.Token
            );
        if (nodes == null)
        {
            NodesCount = 0;
            return;
        }
        NodesCount = nodes.Lines.Count;
    }

    [RelayCommand]
    async Task AddUserAsync()
    {
        await DialogManager.ShowWebGameDialogAsync();
    }


    async partial void OnSelectLoginChanged(CloudGameLoginSession value)
    {
        if (value == null)
            return;
        var result =
            await this.KuroCloudGameContext.WavesCloudSurivivalService.WavesCloudGameService.GetWalletDataAsync(
                value,
                this.CTS.Token
            );
        WallDataWrapper wrapper = new();
        wrapper.FreeTime = TimeSpan.FromSeconds(result.Data.FreeTimeInfo.LeftSeconds);
        wrapper.PlayerCard = DateTimeOffset.FromUnixTimeSeconds(
            result.Data.TimeCardInfo.ExpireTimeSeconds
        );

        wrapper.PayTimer = TimeSpan.FromSeconds(result.Data.PayTimeInfo.LeftSeconds);
        if (result.Data.ExperienceCardInfo != null)
            wrapper.ExperienceTime = new TimeSpan(
                result.Data.ExperienceCardInfo.Day,
                result.Data.ExperienceCardInfo.Hour,
                result.Data.ExperienceCardInfo.Minute,
                result.Data.ExperienceCardInfo.Second
            );
        wrapper.Coin = result.Data.Coin;
        this.WallData = wrapper;
    }

}
