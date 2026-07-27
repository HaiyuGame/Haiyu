namespace Haiyu.Models.Wrapper;

public partial class StaminaWrapper : ObservableObject
{
    public StaminaWrapper(GamerBassData data)
    {
        try
        {
            this.Energy = data.Energy;
            var cTime = DateTimeOffset.FromUnixTimeMilliseconds((long)(data.CreatTime)).ToLocalTime().DateTime;
            var timeCali = cTime - new DateTime(2024, 5, 23);
            if (timeCali.TotalDays <= 10)
            {
                IsShowTime = Visibility.Visible;
            }
            this.CreateTime = LanguageService.FormatByText(LanguageService.GetStringByText("{0}年{1}月{2}日   {3}:{4}:{5}"), cTime.Year, cTime.Month, cTime.Day, cTime.Hour, cTime.Minute, cTime.Second);
            this.MaxEnergy = data.MaxEnergy;
            this.StoreEnergy = data.StoreEnergy;
            this.MaxStoreEnergy = data.StoreEnergyLimit;
            this.Liveness = data.Liveness;
            this.MaxLiveness = data.LivenessMaxCount;
            this.WeekyInstCount = data.WeeklyInstCount;
            this.WeekyInstIcon = data.WeeklyInstIconUrl;
            this.MaxWeekyInstCount = data.WeeklyInstCountLimit;
            this.RougeScore = data.RougeScore;
            this.MaxRougeScore = data.RougeScoreLimit;
            this.EnergyIconUrl = data.StoreEnergyIconUrl;
            this.WeekyInstIcon = data.WeeklyInstIconUrl;
            this.Name = data.Name;
            this.UserLevel = data.Level;
            this.ActiveDays = data.ActiveDays;
            this.BoxList = data
                .BoxList.Concat(
                    data.PhantomBoxList.Select(x => new BoxList() { BoxName = x.Name, Num = x.Num })
                )
                .Concat(
                    data.TreasureBoxList.Select(x => new BoxList()
                    {
                        BoxName = x.Name,
                        Num = x.Num,
                    })
                )
                .ToList();
        }
        catch (Exception) { }
    }

    #region User
    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial int UserLevel { get; set; }

    [ObservableProperty]
    public partial List<BoxList> BoxList { get; set; }

    [ObservableProperty]
    public partial Visibility IsShowTime { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial string CreateTime { get; set; }

    [ObservableProperty]
    public partial int ActiveDays { get; set; }
    #endregion

    #region 结晶波片

    [ObservableProperty]
    public partial int Energy { get; set; }

    [ObservableProperty]
    public partial int MaxEnergy { get; set; }

    #endregion

    #region 结晶单质
    [ObservableProperty]
    public partial int StoreEnergy { get; set; }

    [ObservableProperty]
    public partial int MaxStoreEnergy { get; set; }
    #endregion

    #region 活跃度
    [ObservableProperty]
    public partial int Liveness { get; set; }

    [ObservableProperty]
    public partial int MaxLiveness { get; set; }
    #endregion

    #region 战歌重奏
    [ObservableProperty]
    public partial int WeekyInstCount { get; set; }

    [ObservableProperty]
    public partial string WeekyInstIcon { get; set; }

    [ObservableProperty]
    public partial string EnergyIconUrl { get; set; }

    [ObservableProperty]
    public partial int MaxWeekyInstCount { get; set; }
    #endregion

    #region 千道门扉的异想

    [ObservableProperty]
    public partial int RougeScore { get; set; }

    [ObservableProperty]
    public partial int MaxRougeScore { get; set; }
    #endregion
}
