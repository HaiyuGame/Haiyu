using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Common;
using Waves.Core.Models.Enums;

namespace Haiyu.ViewModel.GameViewModels.GameContexts
{
    public partial class PunishGameContextViewModel : KuroGameContextViewModel
    {

        public PunishGameContextViewModel(IAppContext<App> appContext, ITipShow tipShow)
            : base(appContext, tipShow)
        {
            WeakReferenceMessenger.Default.Register<LocalGameRefreshBindUser>(
                this,
                LocalGameRefreshBindUserMethod
            );
        }



        /// <summary>
        /// 是否正在刷新本地账户状态
        /// </summary>
        [ObservableProperty]
        public partial bool IsLocalUserRefresh { get; set; }

        /// <summary>
        /// 本地账户标题信息
        /// </summary>
        [ObservableProperty]
        public partial string LocalUserTitle { get; set; }

        private async void LocalGameRefreshBindUserMethod(object recipient, LocalGameRefreshBindUser message)
        {
            await this.RefreshLocalGameUser(message.data);
        }

        [RelayCommand]
        private async Task RefreshLocalGameUser(KRSDKLauncherCacheWrapper wrapper)
        {
            var lastSelect = await this.GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.LasterSelectLocalUser,
                this.CTS.Token
            );
            if (lastSelect == null)
            {
                return;
            }
            KRSDKLauncherCacheWrapper? selectItem = null;
            if (wrapper != null)
            {
                selectItem = wrapper;
            }
            else
            {
                var localUsers = await this.GameContext.GetLocalGameOAuthAsync(this.CTS.Token);
                if (localUsers == null || localUsers.Count == 0)
                {
                    LocalUserTitle = "请选择账号";
                    
                    return;
                }
                foreach (var item in localUsers)
                {
                    var code = KrKeyHelper.Xor(item.OauthCode, 5);
                    var userPlayers = await GameContext.QueryPlayerInfoAsync(code);
                    if (userPlayers == null || userPlayers.Code != 0)
                    {
                        LocalUserTitle = "获取账号信息失败";
                        
                        await TipShow.ShowMessageAsync("请重新进入游戏获取信息", Symbol.Clear);
                        IsLocalUserRefresh = false;
                        return;
                    }
                    foreach (var player in userPlayers.Items)
                    {
                        KRSDKLauncherCacheWrapper info = new KRSDKLauncherCacheWrapper(item, (PunishQueryPlayerItem)player);
                        if (info.GetKey == lastSelect)
                        {
                            selectItem = info;
                            break;
                        }
                    }
                }

            }
            if (selectItem == null)
            {
                LocalUserTitle = "请选择账号";
                IsLocalUserRefresh = false;
                return;
            }
            var playerItem = (PunishQueryPlayerItem)selectItem.PlayerItem;
            LocalUserTitle = playerItem.RoleName;
            var result = await this.GameContext.QueryRoleInfoAsync(
                KrKeyHelper.Xor(selectItem.Cache.OauthCode, 5),
                playerItem.Id,
                playerItem.ServerName
            );
            if (result == null || result.Items == null || result.Items.Count == 0)
            {
                LocalUserTitle = "获取账号信息失败";
                await TipShow.ShowMessageAsync("请重新进入游戏获取信息", Symbol.Clear);
                IsLocalUserRefresh = false;
                return;
            }
            var punishData = result.Items[0] as PunishLocalGameRoleItem;
            IsLocalUserRefresh = false;
        }

        public override void DisposeAfter()
        {
            if (this.Contents != null)
                this.Contents.Clear();
            if (this.Activity != null)
            {
                this.Activity.Contents.Clear();
                this.Activity.Contents = null;
            }
            if (this.Notice != null)
            {
                this.Notice.Contents.Clear();
                this.Notice.Contents = null;
            }
            if (this.News != null)
            {
                this.News.Contents.Clear();
                this.News.Contents = null;
            }
            if (this.SlideShows != null)
            {
                this.SlideShows.Clear();
                this.SlideShows = null;
            }
        }

        public override Task LoadAfter()
        {
            return Task.CompletedTask;
        }

        [ObservableProperty]
        public partial ObservableCollection<Slideshow> SlideShows { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<string> Tabs { get; set; } =
            new ObservableCollection<string>() { "活动", "公告", "新闻" };

        [ObservableProperty]
        public partial string SelectTab { get; set; }

        partial void OnSelectTabChanged(string value)
        {
            if (value == null)
            {
                Contents.Clear();
                return;
            }
            if (value == Tabs[0])
            {
                if (Contents == null)
                    return;
                Contents = Activity.Contents.ToObservableCollection();
            }
            else if (value == Tabs[1])
            {
                if (Notice == null)
                    return;
                Contents = Notice.Contents.ToObservableCollection();
            }
            else if (value == Tabs[2])
            {
                if (Contents == null)
                    return;
                Contents = News.Contents.ToObservableCollection();
            }
        }

        #region Datas
        public Notice Notice { get; private set; }
        public News News { get; private set; }
        public Waves.Api.Models.Activity Activity { get; private set; }

        [ObservableProperty]
        public partial Visibility PlayerCardVisibility { get; set; }
        #endregion

        [ObservableProperty]
        public partial ObservableCollection<Content> Contents { get; set; } = new();

        public override GameType GameType => GameType.Punish;

        public override async Task ShowCardAsync(bool showCard)
        {
            if (showCard)
            {
                var starter = await this.GameContext.GetLauncherStarterAsync(this.CTS.Token);
                if (starter == null)
                    return;
                this.SlideShows = starter.Slideshow.ToObservableCollection();
                this.Notice = starter.Guidance.Notice;
                this.News = starter.Guidance.News;
                this.Activity = starter.Guidance.Activity;
                PlayerCardVisibility = Visibility.Visible;
                this.SelectTab = null;
                this.SelectTab = Tabs[0];
            }
            else
            {
                this.SelectTab = null;
                PlayerCardVisibility = Visibility.Collapsed;
            }
        }
    }
}
