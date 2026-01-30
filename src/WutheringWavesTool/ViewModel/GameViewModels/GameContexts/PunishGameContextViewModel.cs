using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Models.Enums;

namespace Haiyu.ViewModel.GameViewModels.GameContexts
{
    public partial class PunishGameContextViewModel: KuroGameContextViewModel
    {
        public PunishGameContextViewModel(IAppContext<App> appContext, ITipShow tipShow)
        : base(appContext, tipShow) { }

        


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
        public partial ObservableCollection<Slideshow> SlideShows { get; set; }=[];

        [ObservableProperty]
        public partial ObservableCollection<string> Tabs { get; set; } = new ObservableCollection<string>() { "活动", "公告", "新闻" };

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
                Contents = Activity.Contents.ToObservableCollection();
            }
            else if (value == Tabs[1])
            {
                Contents = Notice.Contents.ToObservableCollection();
            }
            else if (value == Tabs[2])
            {
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

        public async override Task ShowCardAsync(bool showCard)
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
