namespace Haiyu.ViewModel;

#nullable enable
public sealed partial class PlayerRecordViewModel : ViewModelBase, IDisposable
{
    public IPlayerRecordContext PlayerRecordContext { get; }

    public IServiceScopeFactory ServiceScopeFactory { get; }
    public IServiceScope Scope { get; }
    public RecordRequest Request { get; private set; }
    public FiveGroupModel? FiveGroup { get; private set; }
    public List<CommunityRoleData>? AllRole { get; private set; }
    public List<CommunityWeaponData>? AllWeapon { get; private set; }
    public List<int> StartRole { get; private set; }
    public List<int> StartWeapons { get; private set; }
    public List<RecordCardItemWrapper>? RoleActivity { get; private set; }
    public List<RecordCardItemWrapper>? WeaponsActivity { get; private set; }
    public List<RecordCardItemWrapper>? RoleResident { get; private set; }
    public List<RecordCardItemWrapper>? WeaponsResident { get; private set; }
    public List<RecordCardItemWrapper>? Beginner { get; private set; }
    public List<RecordCardItemWrapper>? BeginnerChoice { get; private set; }
    public List<RecordCardItemWrapper>? GratitudeOrientation { get; private set; }

    [ObservableProperty]
    public partial bool IsLoadRecord { get; set; } = false;

    [ObservableProperty]
    public partial CardPoolType? SelectType { get; set; } = null;

    #region 抽卡数据
    [ObservableProperty]
    public partial int AllCount { get; set; }

    [ObservableProperty]
    public partial double ActivityAvg { get; set; }

    [ObservableProperty]
    public partial double ResidentAvg { get; set; }

    [ObservableProperty]
    public partial double Guaranteed { get; set; }

    [ObservableProperty]
    public partial double ScoreValue { get; set; }

    [ObservableProperty]
    public partial double StarAvg { get; set; }

    [ObservableProperty]
    public partial string Id { get; set; }

    [ObservableProperty]
    public partial string CreateTime { get; set; }
    #endregion

    public PlayerRecordViewModel(IServiceScopeFactory serviceScopeFactory)
    {
        ServiceScopeFactory = serviceScopeFactory;
        this.Scope = ServiceScopeFactory.CreateScope();
        this.PlayerRecordContext =
            Scope.ServiceProvider.GetRequiredKeyedService<IPlayerRecordContext>("PlayerRecord");
        this.PlayerRecordContext.SetScope(this.Scope);
    }

    [ObservableProperty]
    public partial ObservableCollection<CardPoolType> CardPools { get; set; } =
        new ObservableCollection<CardPoolType>()
        {
            CardPoolType.RoleActivity,
            CardPoolType.WeaponsActivity,
            CardPoolType.RoleResident,
            CardPoolType.WeaponsResident,
            CardPoolType.Beginner,
            CardPoolType.BeginnerChoice,
            CardPoolType.GratitudeOrientation,
        };

    [RelayCommand]
    async Task ShowInputRecordAsync()
    {
        //var link = await PlayerRecordContext.ShowInputRecordAsync(null);
        //if (link == null)
        //{
        //    return;
        //}
        //switch (link.Type)
        //{
        //    case CreateRecordType.None:
        //        break;
        //    case CreateRecordType.Create:
        //    case CreateRecordType.Update:
        //        if (string.IsNullOrWhiteSpace(link.Link))
        //        {
        //            this.PlayerRecordContext.TipShow.ShowMessage(
        //                "抽卡链接无效",
        //                Microsoft.UI.Xaml.Controls.Symbol.Clear
        //            );
        //            this.SelectType = null;
        //            this.IsLoadRecord = false;
        //            return;
        //        }
        //        var request = RecordHelper.GetRecorRequest(link.Link);
        //        if (request == null)
        //        {
        //            this.PlayerRecordContext.TipShow.ShowMessage(
        //                "抽卡链接无效",
        //                Microsoft.UI.Xaml.Controls.Symbol.Clear
        //            );
        //            this.SelectType = null;
        //            this.IsLoadRecord = false;
        //            return;
        //        }
        //        this.Request = request;
        //        var items = await RecordHelper.GetRecordAsync(Request, CardPoolType.RoleActivity);
        //        if (items == null)
        //        {
        //            this.PlayerRecordContext.TipShow.ShowMessage(
        //                "抽卡链接过期",
        //                Microsoft.UI.Xaml.Controls.Symbol.Clear
        //            );
        //            this.SelectType = null;
        //            this.IsLoadRecord = false;
        //            return;
        //        }
        //        var localRecord = (
        //            await PlayerRecordContext.RecordCacheService.GetRecordCacheDetilyAndPathAsync()
        //        )
        //            .Where(x => x.Item1?.Id == Request.PlayerId)
        //            .FirstOrDefault();
        //        if (localRecord.Item1 == null)
        //        {
        //            if (await WriteCacheAsync())
        //            {
        //                this.FiveGroup = await RecordHelper.GetFiveGroupAsync();
        //                this.AllRole = await RecordHelper.GetAllRoleAsync();
        //                this.AllWeapon = await RecordHelper.GetAllWeaponAsync();
        //                this.StartRole = RecordHelper.FormatFiveRoleStar(FiveGroup);
        //                this.StartWeapons = RecordHelper.FormatFiveWeaponeRoleStar(FiveGroup);
        //                CalculateRange();
        //                this.IsLoadRecord = true;
        //                SelectType = CardPoolType.RoleActivity;
        //            }
        //            else
        //            {
        //                this.PlayerRecordContext.TipShow.ShowMessage(
        //                    "写入抽卡缓存失败",
        //                    Microsoft.UI.Xaml.Controls.Symbol.Clear
        //                );
        //                this.SelectType = null;
        //                this.IsLoadRecord = false;
        //            }
        //        }
        //        else
        //        {
        //            this.FiveGroup = await RecordHelper.GetFiveGroupAsync();
        //            this.AllRole = await RecordHelper.GetAllRoleAsync();
        //            this.AllWeapon = await RecordHelper.GetAllWeaponAsync();
        //            this.StartRole = RecordHelper.FormatFiveRoleStar(FiveGroup);
        //            this.StartWeapons = RecordHelper.FormatFiveWeaponeRoleStar(FiveGroup);
        //            if (await MergeRecordAsync(localRecord))
        //            {
        //                CalculateRange();
        //                this.IsLoadRecord = true;
        //                SelectType = CardPoolType.RoleActivity;
        //            }
        //        }
        //        break;
        //    case CreateRecordType.SelectItemOpen:
        //        if (link.Cache == null)
        //        {
        //            this.IsLoadRecord = false;
        //            SelectType = null;
        //            return;
        //        }
        //        this.FiveGroup = await RecordHelper.GetFiveGroupAsync();
        //        this.AllRole = await RecordHelper.GetAllRoleAsync();
        //        this.AllWeapon = await RecordHelper.GetAllWeaponAsync();
        //        this.StartRole = RecordHelper.FormatFiveRoleStar(FiveGroup);
        //        this.StartWeapons = RecordHelper.FormatFiveWeaponeRoleStar(FiveGroup);
        //        RoleActivity = link.Cache.RoleActivityItems.ToList();
        //        WeaponsActivity = link.Cache.WeaponsActivityItems.ToList();
        //        WeaponsResident = link.Cache.WeaponsResidentItems.ToList();
        //        RoleResident = link.Cache.RoleResidentItems.ToList();
        //        Beginner = link.Cache.BeginnerItems.ToList();
        //        BeginnerChoice = link.Cache.BeginnerChoiceItems.ToList();
        //        GratitudeOrientation = link.Cache.GratitudeOrientationItems.ToList();
        //        CalculateRange();
        //        this.IsLoadRecord = true;
        //        SelectType = CardPoolType.RoleActivity;
        //        this.CreateTime = (DateTime.Now - link.Cache.Time).ToString("hh\\:mm\\:ss");
        //        this.Id = link.Cache.Id;
        //        break;
        //}
    }

    private async Task<bool> MergeRecordAsync((RecordCacheDetily?, string?) localRecord)
    {
        //RoleActivity = await RecordHelper.GetRecordAsync(this.Request, CardPoolType.RoleActivity);
        //WeaponsActivity = await RecordHelper.GetRecordAsync(
        //    this.Request,
        //    CardPoolType.WeaponsActivity
        //);
        //RoleResident = await RecordHelper.GetRecordAsync(this.Request, CardPoolType.RoleResident);
        //WeaponsResident = await RecordHelper.GetRecordAsync(
        //    this.Request,
        //    CardPoolType.WeaponsResident
        //);
        //Beginner = await RecordHelper.GetRecordAsync(this.Request, CardPoolType.Beginner);
        //BeginnerChoice = await RecordHelper.GetRecordAsync(
        //    this.Request,
        //    CardPoolType.BeginnerChoice
        //);
        //GratitudeOrientation = await RecordHelper.GetRecordAsync(
        //    this.Request,
        //    CardPoolType.GratitudeOrientation
        //);
        //if (
        //    RoleActivity == null
        //    || WeaponsActivity == null
        //    || RoleResident == null
        //    || WeaponsResident == null
        //    || Beginner == null
        //    || BeginnerChoice == null
        //    || GratitudeOrientation == null
        //)
        //{
        //    this.PlayerRecordContext.TipShow.ShowMessage("数据拉取失败!", Symbol.Clear);
        //    return false;
        //}
        //var guid = Guid.NewGuid();
        //var cache = new RecordCacheDetily()
        //{
        //    Guid = guid.ToString(),
        //    Name = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:FFFF")}",
        //    Time = DateTime.Now,
        //    Id = this.Request.PlayerId,
        //    RoleActivityItems = RoleActivity,
        //    RoleResidentItems = RoleResident,
        //    WeaponsActivityItems = WeaponsActivity,
        //    WeaponsResidentItems = WeaponsResident,
        //    BeginnerItems = Beginner,
        //    BeginnerChoiceItems = BeginnerChoice,
        //    GratitudeOrientationItems = GratitudeOrientation,
        //};
        //var newCache = RecordHelper.MargeRecord(cache, localRecord);
        //if (newCache == null)
        //{
        //    this.PlayerRecordContext.TipShow.ShowMessage("去重失败", Symbol.Clear);
        //    return false;
        //}
        //else
        //{
        //    RoleActivity = newCache.Item1!.RoleActivityItems.ToList();
        //    WeaponsActivity = newCache.Item1!.WeaponsActivityItems.ToList();
        //    RoleResident = newCache.Item1!.RoleResidentItems.ToList();
        //    WeaponsResident = newCache.Item1!.WeaponsResidentItems.ToList();
        //    Beginner = newCache.Item1!.BeginnerItems.ToList();
        //    BeginnerChoice = newCache.Item1!.BeginnerChoiceItems.ToList();
        //    GratitudeOrientation = newCache.Item1!.GratitudeOrientationItems.ToList();
        //    File.Delete(newCache.Item2);
        //    await this.PlayerRecordContext.RecordCacheService.CreateRecordAsync(newCache.Item1);
        //    this.CreateTime = (DateTime.Now - newCache.Item1.Time).ToString("hh\\:mm\\:ss");
        //    this.Id = newCache.Item1.Id;
        //    this.PlayerRecordContext.TipShow.ShowMessage("合并抽卡成功", Symbol.Accept);
        //}
        return true;
    }

    private async Task<bool> WriteCacheAsync()
    {
        RoleActivity = await RecordHelper.GetRecordAsync(this.Request, CardPoolType.RoleActivity);
        WeaponsActivity = await RecordHelper.GetRecordAsync(
            this.Request,
            CardPoolType.WeaponsActivity
        );
        RoleResident = await RecordHelper.GetRecordAsync(this.Request, CardPoolType.RoleResident);
        WeaponsResident = await RecordHelper.GetRecordAsync(
            this.Request,
            CardPoolType.WeaponsResident
        );
        Beginner = await RecordHelper.GetRecordAsync(this.Request, CardPoolType.Beginner);
        BeginnerChoice = await RecordHelper.GetRecordAsync(
            this.Request,
            CardPoolType.BeginnerChoice
        );
        GratitudeOrientation = await RecordHelper.GetRecordAsync(
            this.Request,
            CardPoolType.GratitudeOrientation
        );
        if (
            RoleActivity == null
            || WeaponsActivity == null
            || RoleResident == null
            || WeaponsResident == null
            || Beginner == null
            || BeginnerChoice == null
            || GratitudeOrientation == null
        )
        {
            this.PlayerRecordContext.TipShow.ShowMessage("数据拉取失败!", Symbol.Clear);
            return false;
        }
        var guid = Guid.NewGuid();
        var cache = new RecordCacheDetily()
        {
            //Guid = guid.ToString(),
            Name = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:FFFF")}",
            Time = DateTime.Now,
            //Id = this.Request.PlayerId,
            RoleActivityItems = RoleActivity,
            RoleResidentItems = RoleResident,
            WeaponsActivityItems = WeaponsActivity,
            WeaponsResidentItems = WeaponsResident,
            BeginnerItems = Beginner,
            BeginnerChoiceItems = BeginnerChoice,
            GratitudeOrientationItems = GratitudeOrientation,
        };
        await this.PlayerRecordContext.RecordCacheService.CreateRecordAsync(cache);

        this.CreateTime = (DateTime.Now - cache.Time).ToString("hh\\:mm\\:ss");
        //this.Id = cache.Id;
        return true;
    }

    private void CalculateRange()
    {
        if (
            FiveGroup == null
            || Beginner == null
            || BeginnerChoice == null
            || GratitudeOrientation == null
            || RoleActivity == null
            || WeaponsActivity == null
            || RoleResident == null
            || WeaponsResident == null
        )
            return;
        this.ActivityAvg = Math.Round(
            RecordHelper
                .FormatRecordFive(this.RoleActivity)
                .Concat(RecordHelper.FormatRecordFive(this.WeaponsActivity))
                .CalculateAvg(),
            2
        );
        this.ResidentAvg = Math.Round(
            RecordHelper
                .FormatRecordFive(this.RoleResident)
                .Concat(RecordHelper.FormatRecordFive(this.WeaponsResident))
                .CalculateAvg(),
            2
        );
        var roleAAvg = RecordHelper.FormatRecordFive(this.RoleActivity).CalculateAvg();
        var weaponAAvg = RecordHelper.FormatRecordFive(this.WeaponsActivity!).CalculateAvg();
        var resident = RecordHelper
            .FormatRecordFive(this.RoleResident!.Concat(this.WeaponsResident!))
            .CalculateAvg();
        //var range = RecordHelper
        //    .FormatStartFive(RoleActivity, RecordHelper.FormatFiveRoleStar(FiveGroup!))!
        //    .GetGuaranteedRange();
        //var value = Math.Round(RecordHelper.Score(range, roleAAvg, weaponAAvg, resident), 2);
        //this.Guaranteed = Math.Round(range, 2);
        //this.ScoreValue = value;
        this.StarAvg = Math.Round(
            RecordHelper
                .FormatRecordFive(this.RoleActivity)
                .Concat(RecordHelper.FormatRecordFive(this.RoleResident))
                .Concat(RecordHelper.FormatRecordFive(this.WeaponsActivity))
                .Concat(RecordHelper.FormatRecordFive(this.WeaponsResident))
                .Concat(RecordHelper.FormatRecordFive(this.WeaponsActivity))
                .CalculateAvg(),
            2
        );
        this.AllCount =
            RoleActivity.Count
            + WeaponsActivity.Count
            + RoleResident.Count
            + WeaponsResident.Count
            + Beginner.Count
            + BeginnerChoice.Count
            + GratitudeOrientation.Count;
    }

    private bool disposedValue;

    partial void OnSelectTypeChanged(CardPoolType? value)
    {
        if (value == null)
            return;
        var arg = new RecordArgs(
            RoleActivity,
            WeaponsActivity,
            RoleResident,
            WeaponsResident,
            Beginner,
            BeginnerChoice,
            GratitudeOrientation
        )
        {
            Request = this.Request,
            Roles = this.StartRole,
            Weapons = this.StartWeapons,
            FiveGroup = this.FiveGroup,
            AllRole = this.AllRole,
            AllWeapon = this.AllWeapon,
            Type = value.Value,
        };
        this.PlayerRecordContext.NavigationService.NavigationTo<RecordItemViewModel>(
            arg,
            new DrillInNavigationTransitionInfo()
        );
    }

    public async Task Loaded(Microsoft.UI.Xaml.Controls.Frame frame = null)
    {
        await this.ShowInputRecordAsync();
    }

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                this.PlayerRecordContext.NavigationService.UnRegisterView();
                this.PlayerRecordContext.Dispose();
                Scope.Dispose();
            }
            disposedValue = true;
        }
    }

    // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
    // ~PlayerRecordViewModel()
    // {
    //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
