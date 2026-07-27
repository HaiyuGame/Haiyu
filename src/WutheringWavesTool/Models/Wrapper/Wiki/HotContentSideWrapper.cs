using Windows.System;

namespace Haiyu.Models.Wrapper.Wiki
{
    public partial class HotContentSideWrapper : ObservableObject
    {
        [ObservableProperty]
        public partial string ImageUrl { get; set; }
        [ObservableProperty]
        public partial string Title { get; set; }
        [ObservableProperty]
        public partial string StartTime { get; set; }
        [ObservableProperty]
        public partial string EndTime { get; set; }
        [ObservableProperty]
        public partial string TotalSpan { get; set; }

        [ObservableProperty]
        public partial double MaxProgress { get; set; }


        [ObservableProperty]
        public partial string Message { get; set; }

        [ObservableProperty]
        public partial Visibility TimeVisibility { get; set; } = Visibility.Visible;

        [ObservableProperty]
        public partial double CurrentProgress { get; set; }


        [ObservableProperty]
        public partial string JumpUrl { get; set; }
        [ObservableProperty]
        public partial string Color { get; set; }

        public IAsyncRelayCommand JumpWebCommand => new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(JumpUrl))
                return;
            await Launcher.LaunchUriAsync(new(JumpUrl));
        });

        public void Cali()
        {
            const int LONG_CONTENT_TIME = 360;
            DateTime now = DateTime.Now;
            DateTime start = DateTime.Parse(StartTime);
            DateTime end = DateTime.Parse(EndTime);

            //活动剩余时间
            TimeSpan _endCountdownTimeSpan = end - now;
            //活动持续时间
            TimeSpan _totalDurationTimeSpan = end - start;
            //活动已完成时间
            TimeSpan _overCountdownTimeSpace = now - start;


            this.MaxProgress = _totalDurationTimeSpan.TotalSeconds;

            double elapsed = _endCountdownTimeSpan.TotalSeconds;



            if (elapsed <= 0)
            {
                Message = LanguageService.GetStringByText("已结束");
                this.Color = "Red";
                this.CurrentProgress = this.MaxProgress;
                return;
            }

            if (elapsed > this.MaxProgress)
            {
                Message = LanguageService.GetStringByText("未开始");
                this.CurrentProgress = 0;
                this.Color = "Gray";
                return;
            }

            if (_totalDurationTimeSpan.TotalDays >= LONG_CONTENT_TIME)
            {
                TimeVisibility = Visibility.Collapsed;
                Message = LanguageService.GetStringByText("长期活动");
                this.CurrentProgress = this.MaxProgress;
                this.Color = "Black";
                return;
            }

            Message = LanguageService.GetStringByText("进行中");
            this.CurrentProgress = _overCountdownTimeSpace.TotalSeconds;
            this.Color = "#3399FF";
            TotalSpan = LanguageService.FormatByText(LanguageService.GetStringByText("剩余{0}天"), _endCountdownTimeSpan.Days) +
                        LanguageService.FormatByText(LanguageService.GetStringByText("{0}小时"), _endCountdownTimeSpan.Hours) +
                        LanguageService.FormatByText(LanguageService.GetStringByText("{0}分"), _endCountdownTimeSpan.Minutes);
        }
    }
}
