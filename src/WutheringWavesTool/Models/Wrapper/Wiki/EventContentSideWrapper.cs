namespace Haiyu.Models.Wrapper.Wiki;

public partial class EventContentSideWrapper : ObservableObject
{
    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string ImgMode { get; set; }

    [ObservableProperty]
    public partial DateTime StartTime { get; set; }


    [ObservableProperty]
    public partial DateTime StopTime { get; set; }

    /// <summary>
    /// 大图
    /// </summary>
    [ObservableProperty]
    public partial string BigImage { get; set; }

    /// <summary>
    /// 小图
    /// </summary>
    [ObservableProperty]
    public partial string Image1 { get; set; }


    [ObservableProperty]
    public partial string Image2 { get; set; }

    [ObservableProperty]
    public partial string Image3 { get; set; }

    [ObservableProperty]
    public partial string Image4 { get; set; }

    [ObservableProperty]
    public partial string DisplayTime { get; set; }

    [ObservableProperty]
    public partial double MaxProgress { get; set; }


    [ObservableProperty]
    public partial double CurrentProgress { get; set; }
    internal void Cali()
    {
        var now = DateTime.Now;
        var totalTime = this.StopTime - this.StartTime;
        var remainingTime = this.StopTime - now;
        if (remainingTime.TotalSeconds < 0)
            remainingTime = TimeSpan.Zero;
        this.MaxProgress = totalTime.TotalSeconds;
        this.CurrentProgress = remainingTime.TotalSeconds;
        this.DisplayTime = LanguageService.FormatByText(LanguageService.GetStringByText("{0}天{1:D2}小时{2:D2}分{3:D2}秒"), remainingTime.Days, remainingTime.Hours, remainingTime.Minutes, remainingTime.Seconds);
    }
}