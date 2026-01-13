using System.Collections.Generic;
using HarfBuzzSharp;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel;
using MemoryPack;
using Waves.Api.Models.CloudGame;
using Waves.Core.Settings;
using ZLinq;

namespace Haiyu.ViewModel;

public partial class AnalysisRecordViewModel : ViewModelBase
{
    private readonly List<RecordCardItemWrapper> roleActivity = new();
    private readonly List<RecordCardItemWrapper> weaponActiviy = new();
    private readonly List<RecordCardItemWrapper> roleDaily = new();
    private readonly List<RecordCardItemWrapper> weaponDaily = new();

    public AnalysisRecordViewModel(ICloudGameService cloudGameService)
    {
        CloudGameService = cloudGameService;
    }

    [ObservableProperty]
    public partial Visibility LoadingVisibility { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<DateTimePoint> AllPoints { get; set; } = new();

    [ObservableProperty]
    public partial Visibility DataVisibility { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set;  }
    public ICloudGameService CloudGameService { get; }
    public LoginData LoginData { get; internal set; }

    [RelayCommand]
    async Task Loaded()
    {
        await RefreshAsync();
    }

    [ObservableProperty]
    public partial long RoleActivityAllCount { get; set; } = 0;

    [ObservableProperty]
    public partial long WeaponActivityAllCount { get; set; } = 0;

    [ObservableProperty]
    public partial long DailyAllCount { get; set; }

    [ObservableProperty]
    public partial long RoleActivityCount { get; set; } = 0;

    [ObservableProperty]
    public partial long WeaponActivityCount { get; set; } = 0;

    [ObservableProperty]
    public partial long RoleActivityCount2 { get; set; } = 0;

    [ObservableProperty]
    public partial double CurrentRoleDeily { get; set; } = 0;

    [ObservableProperty]
    public partial double CurrentWeaponDeily { get; set; } = 0;

    [ObservableProperty]
    public partial long ExpectedRoleDeily { get; set; } = 0;

    [ObservableProperty]
    public partial long ExpectedActivityWeaponDeily { get; set; } = 0;

    [ObservableProperty]
    public partial long ExpectedActivityRoleDeily { get; set; } = 0;

    [ObservableProperty]
    public partial long ExpectedDailyWeaponDeily { get; set; } = 0;

    [ObservableProperty]
    public partial ObservableCollection<GameRecordNavigationItem> RecordNavigationItems { get; set; } =
        GameRecordNavigationItem.FourDefault;

    [ObservableProperty]
    public partial GameRecordNavigationItem SelectNavigationItem { get; set; }

    [ObservableProperty]
    public partial double Guaranteed { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<RecordActivityFiveStarItemWrapper> StarItems { get; set; } =
    [];
    public FiveGroupModel FiveGroup { get; private set; }
    public List<CommunityRoleData> AllRole { get; private set; }
    public List<CommunityWeaponData> AllWeapon { get; private set; }
    public List<int> StartRole { get; private set; }
    public List<int> StartWeapons { get; private set; }

    /// <summary>
    /// 星级分布
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<object> StarPipeDatas { get; set; } = new();

    [ObservableProperty]
    public partial double AvgCount { get; set; } = 0;

    /// <summary>
    /// 大保底与小保底占比
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<object> RangePipeDatas { get; set; } = new();

    partial void OnSelectNavigationItemChanged(GameRecordNavigationItem value)
    {
        if (value == null)
            return;
        AllPoints.Clear();
        int lastCount = 0;
        switch (value.Id)
        {
            case 1:
                StarItems = RecordHelper
                    .FormatStartFive(
                        this.roleActivity,
                        out lastCount,
                        RecordHelper.FormatFiveRoleStar(this.FiveGroup)
                    )
                    .Item1.Format(this.AllRole, true)
                    .Reverse()
                    .ToObservableCollection();

                break;
            case 2:
                StarItems = RecordHelper
                    .FormatStartFive(
                        this.weaponActiviy,
                        out lastCount,
                        RecordHelper.FormatFiveWeaponeRoleStar(this.FiveGroup)
                    )
                    .Item1.Format(this.AllWeapon, false)
                    .Reverse()
                    .ToObservableCollection();
                break;
            case 3:
                StarItems = RecordHelper
                    .FormatStartFive(
                        this.roleDaily,
                        out lastCount,
                        RecordHelper.FormatFiveRoleStar(this.FiveGroup)
                    )
                    .Item1.Format(this.AllRole, false)
                    .Reverse()
                    .ToCardItemObservableCollection();
                break;
            case 4:
                StarItems = RecordHelper
                    .FormatStartFive(
                        this.weaponDaily,
                        out lastCount,
                        RecordHelper.FormatFiveRoleStar(this.FiveGroup)
                    )
                    .Item1.Format(this.AllWeapon, false)
                    .Reverse()
                    .ToCardItemObservableCollection();
                break;
        }
        if (StarItems != null && StarItems.Count > 0)
        {
            StarItems.Insert(
                0,
                new RecordActivityFiveStarItemWrapper()
                {
                    Icon = null,
                    Flage = false,
                    Count = lastCount,
                    ShowFlage = Visibility.Collapsed,
                    Name = $"已经垫了{lastCount}发",
                }
            );
            AvgCount = Math.Round(StarItems.Select(x => x.Count).Average(), 2);
        }
        else
        {
            AvgCount = 0;
        }
    }

    [RelayCommand]
    async Task RefreshAsync()
    {
        try
        {
            IsLoading = true;
            LoadingVisibility = Visibility.Visible;
            DataVisibility = Visibility.Collapsed;
            var cachePath = AppSettings.RecordFolder + $"\\{this.LoginData.Username}.json";
            int lastCount = 0;
            if (cachePath == null)
            {
                LoadingVisibility = Visibility.Collapsed;
                DataVisibility = Visibility.Collapsed;
                WindowExtension.MessageBox(
                    IntPtr.Zero,
                    "抽卡获取失败！请尝试重新登陆云鸣潮！",
                    "数据错误",
                    0
                );
                IsLoading = false;
                return;
            }
            var datas = MemoryPackSerializer.Deserialize<RecordCacheDetily>(
                await File.ReadAllBytesAsync(cachePath),
                new MemoryPackSerializerOptions() { StringEncoding = StringEncoding.Utf8 }
            );
            if (datas == null)
            {
                LoadingVisibility = Visibility.Collapsed;
                DataVisibility = Visibility.Collapsed;
                WindowExtension.MessageBox(
                    IntPtr.Zero,
                    "抽卡获取失败！请尝试重新登陆云鸣潮！",
                    "数据错误",
                    0
                );
                IsLoading = false;
                return;
            }
            #region 刷新数据源
            roleActivity.Clear();
            weaponActiviy.Clear();
            roleDaily.Clear();
            weaponDaily.Clear();
            RoleActivityAllCount = 0;
            RoleActivityCount = 0;
            RoleActivityCount2 = 0;
            WeaponActivityAllCount = 0;
            WeaponActivityCount = 0;
            roleActivity.AddRange(datas.RoleActivityItems ?? []);
            weaponActiviy.AddRange(datas.WeaponsActivityItems ?? []);
            roleDaily.AddRange(datas.RoleResidentItems ?? []);
            weaponDaily.AddRange(datas.WeaponsResidentItems ?? []);
            #endregion
            FiveGroup = await RecordHelper.GetFiveGroupAsync();
            AllRole = await RecordHelper.GetAllRoleAsync();
            AllWeapon = await RecordHelper.GetAllWeaponAsync();
            StartRole = RecordHelper.FormatFiveRoleStar(FiveGroup);
            StartWeapons = RecordHelper.FormatFiveWeaponeRoleStar(FiveGroup);
            #region 计算
            RoleActivityAllCount = roleActivity.Count;
            WeaponActivityAllCount = weaponActiviy.Count;
            DailyAllCount = roleDaily.Count + weaponDaily.Count;
            var ruleActiv = RecordHelper.FormatRecordFive(this.roleActivity);
            var weaponActiv = RecordHelper.FormatRecordFive(this.weaponActiviy);
            var weaponDail = RecordHelper.FormatRecordFive(this.weaponDaily);
            var roleDail = RecordHelper.FormatRecordFive(this.roleDaily);
            var allData = roleActivity.Concat(weaponActiviy).Concat(roleDaily).Concat(weaponDaily);
            this.StarPipeDatas.Clear();

            #region 星级占比
            StarPipeDatas.Add(
                new PieData()
                {
                    Name = "3星",
                    Offset = 0,
                    Values = [allData.Where(x => x.QualityLevel == 3).Count()],
                }
            );
            StarPipeDatas.Add(
                new PieData()
                {
                    Name = "4星",
                    Offset = 0,
                    Values = [allData.Where(x => x.QualityLevel == 4).Count()],
                }
            );
            StarPipeDatas.Add(
                new PieData()
                {
                    Name = "5星",
                    Offset = 0,
                    Values = [allData.Where(x => x.QualityLevel == 5).Count()],
                }
            );
            #endregion

            #region 歪不歪
            if (roleActivity.Count > 0)
            {
                var roleRange = RecordHelper.FormatStartFive(
                    roleActivity,
                    out lastCount,
                    RecordHelper.FormatFiveRoleStar(FiveGroup!)
                );
                var passValue = roleRange.Item1.Where(x => x.Item3 == false);
                var ngValue = roleRange.Item1.Where(x => x.Item3 == true);
                int passCount = passValue.Count();
                int ngCount = ngValue.Count();
                RangePipeDatas.Add(
                        new PieData()
                        {
                            Name = "歪了",
                            Offset = 0,
                            Values = [ngCount],
                        }
                    );
                RangePipeDatas.Add(
                    new PieData()
                    {
                        Name = "中了",
                        Offset = 0,
                        Values = [passValue.Count()],
                    }
                );
                this.ExpectedActivityRoleDeily = (long)(80 - roleRange.Item2);
                this.Guaranteed = Math.Round(RecordHelper.GetGuaranteedRange(roleRange.Item1), 2);
            }
            #endregion
            foreach (var item in ruleActiv)
            {
                if (
                    FiveGroup
                        .Data.FiveGroupConfig.FiveMaps.Where(x => x.ItemId == item.Item1.ResourceId)
                        .Any()
                )
                {
                    RoleActivityCount++;
                }
                else
                {
                    RoleActivityCount2++;
                }
            }
            if (weaponActiv.Count != 0)
            {
                var weaponRange = RecordHelper.FormatStartFive(
                    weaponDaily,
                    out lastCount,
                    RecordHelper.FormatFiveRoleStar(FiveGroup!)
                );
                this.WeaponActivityCount = weaponActiv.Count;
                ExpectedDailyWeaponDeily = (long)(80 - weaponRange.Item2);
            }
            #endregion
            if (roleDail.Count != 0)
            {
                var RoleRange = RecordHelper.FormatStartFive(
                    roleDaily,
                    out lastCount,
                    RecordHelper.FormatFiveRoleStar(FiveGroup!)
                );
                ExpectedRoleDeily = (long)(80 - RoleRange.Item2);
                CurrentRoleDeily = (double)RoleRange.Item2;
            }
            if (weaponDail.Count != 0)
            {
                var weaponRange = RecordHelper.FormatStartFive(
                    weaponActiviy,
                    out lastCount,
                    RecordHelper.FormatFiveRoleStar(FiveGroup!)
                );
                this.WeaponActivityCount = weaponActiv.Count;
                ExpectedActivityWeaponDeily = (long)(80 - weaponRange.Item2);
                CurrentWeaponDeily = (double)weaponRange.Item2;
            }
            LoadingVisibility = Visibility.Collapsed;
            DataVisibility = Visibility.Visible;
            this.SelectNavigationItem = null;
            this.SelectNavigationItem = this.RecordNavigationItems[0];
            IsLoading = false;
        }
        catch (Exception ex)
        {
            WindowExtension.MessageBox(IntPtr.Zero, $"抽卡获取失败！{ex.Message}", "数据错误", 0);
        }
    }
}


