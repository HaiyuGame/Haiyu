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
            if(string.IsNullOrWhiteSpace(this.PlayerCardString) || this.PlayerCardString == "1970-01-01")
                this.PayString = $"{value.Days}:{value.Hours}:{value.Minutes}";
            field = value;
        }
    }

    public DateTimeOffset PlayerCard
    {
        get => field;
        set
        {
            this.PlayerCardString = value.ToString("yyyy-MM-dd");
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
