using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Models.Wrapper;

public partial class WallDataWrapper:ObservableObject
{
    [ObservableProperty]
    public partial string FreeString { get; set; }

    [ObservableProperty]
    public partial string PlayerCardString { get; set; }

    [ObservableProperty]
    public partial string ExperienseTimeString { get; set; }

    [ObservableProperty]
    public partial string PayString { get; set; }

    [ObservableProperty]
    public partial int Coin { get; set; }

    public TimeSpan FreeTime
    {
        get => field;
        set
        {
            this.FreeString = $"{Math.Round(value.TotalMinutes,2)}";
            field = value;
        }
    }
    public TimeSpan ExperienceTime
    {
        get=> field;
        set
        {
            this.ExperienseTimeString = DisplayTimeFormatter.FormatDuration(value);
            field = value;
        }
    }

    public DateTimeOffset PlayerCard
    {
        get => field;
        set
        {
            this.PlayerCardString = DisplayTimeFormatter.FormatDate(value.LocalDateTime);
            field = value;
        }
    }
    public TimeSpan PayTimer
    {
        get => field;
        set
        {
            this.PayString = $"{Math.Round(value.TotalMinutes, 2)}";
            field = value;
        }
    }
}
