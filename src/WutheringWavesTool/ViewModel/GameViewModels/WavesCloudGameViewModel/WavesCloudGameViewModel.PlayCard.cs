using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Common;
using Waves.Core.Models.CloudGame;

namespace Haiyu.ViewModel.GameViewModels;

partial class WavesCloudGameViewModel
{
    [ObservableProperty]
    public partial string UserName { get; set; }

    private string _userId;

    [ObservableProperty]
    public partial bool IsLogin { get; set; }

    async Task RefreshUserAsync()
    {
        var refreshVersion = Interlocked.Increment(ref this._wallDataRefreshVersion);
        var session = await this.WavesCloudGameService.GetCurrentUserSession();
        if (session == null)
        {
            this.WallData = CreateEmptyWallData();
            this.UserName = string.Empty;
            this._userId = string.Empty;
            this.IsLogin = false;
            return;
        }
        this.UserName = session.OrginData.Username;
        this._userId = session.GetId();
        var result =
            await this.WavesCloudGameService.GetWalletDataAsync(
                session,
                this.CTS.Token
            );
        if (result is null || result.Data is null)
        {
            // 接口失败或响应无数据：仅提示，保留界面现有数据与登录态
            await WindowManager.Shell.TipShow.ShowMessageAsync(
                LanguageService.GetStringByText("获取余额失败！"),
                Symbol.Clear
            );
            return;
        }
        WallDataWrapper wrapper = new();
        wrapper.FreeTime = TimeSpan.FromSeconds(result.Data.FreeTimeInfo?.LeftSeconds ?? 0);
        // 服务器在畅享卡过期后仍原样返回过期的 expireTimeSeconds，这里只在未过期时显示：
        // 已过期或为 0/缺失（无卡）时不赋值，畅享卡状态置空
        var timeCardExpireSeconds = result.Data.TimeCardInfo?.ExpireTimeSeconds ?? 0;
        if (timeCardExpireSeconds > 0)
        {
            var expireTime = DateTimeOffset.FromUnixTimeSeconds(timeCardExpireSeconds);
            if (expireTime.LocalDateTime > DateTime.Now)
            {
                wrapper.PlayerCard = expireTime;
            }
        }

        wrapper.PayTimer = TimeSpan.FromSeconds(result.Data.PayTimeInfo?.LeftSeconds ?? 0);
        if (result.Data.ExperienceCardInfo != null)
            wrapper.ExperienceTime = new TimeSpan(
                result.Data.ExperienceCardInfo.Day,
                result.Data.ExperienceCardInfo.Hour,
                result.Data.ExperienceCardInfo.Minute,
                result.Data.ExperienceCardInfo.Second
            );
        wrapper.Coin = result.Data.Coin;

        if (refreshVersion != Volatile.Read(ref this._wallDataRefreshVersion))
            return;

        this.WallData = wrapper;
        this.IsLogin = true;
    }

    [RelayCommand]
    async Task RefreshCardAsync()
    {
        IsRefreshing = true;
        if (this.KuroCloudGameContext == null)
        {
            await WindowManager.Shell.TipShow.ShowMessageAsync(LanguageService.GetStringByText("游戏核心为空！请尝试刷新页面"),Symbol.Clear);
            return;
        }
        await this.RefreshUserAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    async Task AddUserAsync()
    {
        await WindowManager.Shell.DialogManager.ShowWebGameDialogAsync();
    }



}
