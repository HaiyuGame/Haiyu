namespace Haiyu.ViewModel;

/// <summary>与窗口和记录来源无关的抽卡统计展示。</summary>
public sealed partial class WavesRecordAnalysisViewModel : ViewModelBase
{
    public FiveGroupModel FiveGroup { get; private set; }
    public List<CommunityRoleData> AllRole { get; private set; }
    public List<CommunityWeaponData> AllWeapon { get; private set; }
    private WavesAnalysisPlayerCard Cards { get; set; }
    private int initializationVersion;

    public WavesRecordAnalysisViewModel() => InitializeCharts();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>宿主传入已经加载或合并的记录；快速切换时仅应用最后一次请求。</summary>
    public async Task InitializeAsync(WavesAnalysisPlayerCard cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        if (IsDisposed)
            return;
        var version = ++initializationVersion;
        var token = LifetimeToken;
        IsLoading = true;
        try
        {
            var fiveGroup = await RecordHelper.GetFiveGroupAsync(token) ?? new();
            var roles = await RecordHelper.GetAllRoleAsync(token) ?? new();
            var weapons = await RecordHelper.GetAllWeaponAsync(token) ?? new();
            if (IsDisposed || version != initializationVersion)
                return;
            token.ThrowIfCancellationRequested();
            FiveGroup = fiveGroup;
            AllRole = roles;
            AllWeapon = weapons;
            Cards = cards;
            Cards.Items ??= [];
            foreach (var pool in Cards.Items)
                pool.Resource ??= [];
            StarItems = new();
            StarAvgValue = 0;
            SelectNavItem = null;
            InitNavItems();
            await AnalysisStarAsync();
        }
        finally
        {
            if (!IsDisposed && version == initializationVersion)
                IsLoading = false;
        }
    }
}
