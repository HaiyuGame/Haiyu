using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Haiyu.Services.DialogServices;
using LiveChartsCore.Defaults;
using MemoryPack;
using Waves.Api.Models.CloudGame;
using Waves.Core.Settings;

namespace Haiyu.ViewModel;

public partial class CloudGameViewModel : ViewModelBase
{
    public CloudGameViewModel(
        ICloudGameService cloudGameService,
        ITipShow tipShow,
        [FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager,
        IViewFactorys viewFactorys,
        IPlayerCardService playerCardService,
        IPickersService pickersService
    )
    {
        CloudGameService = cloudGameService;
        TipShow = tipShow;
        DialogManager = dialogManager;
        ViewFactorys = viewFactorys;
        PlayerCardService = playerCardService;
        PickersService = pickersService;
        RegisterMananger();
    }

    private void RegisterMananger()
    {
        this.Messenger.Register<CloudLoginMessager>(this, CloudLoginMethod);
    }

    private async void CloudLoginMethod(object recipient, CloudLoginMessager message)
    {
        if (message == null || !message.Refresh)
            return;
        await this.Loaded();
    }

    public ICloudGameService CloudGameService { get; }
    public ITipShow TipShow { get; }
    public IDialogManager DialogManager { get; }
    public IViewFactorys ViewFactorys { get; }
    public IPlayerCardService PlayerCardService { get; }
    public IPickersService PickersService { get; }

    private List<RecordCardItemWrapper> cacheItems;

    [ObservableProperty]
    public partial long PageSize { get; set; } = 10;

    [ObservableProperty]
    public partial ObservableCollection<RecordCardItemWrapper> ResourceItems { get; set; } =
        new ObservableCollection<RecordCardItemWrapper>();

    public readonly Dictionary<int, IList<RecordCardItemWrapper>> aLLcacheItems =
        new Dictionary<int, IList<RecordCardItemWrapper>>();

    [ObservableProperty]
    public partial ObservableCollection<LoginData> Users { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<GameRecordNavigationItem> RecordNavigationItems { get; set; } =
        GameRecordNavigationItem.Default;

    [ObservableProperty]
    public partial GameRecordNavigationItem SelectRecordType { get; set; }

    [ObservableProperty]
    public partial LoginData SelectedUser { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial Visibility LoadVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility DataVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility NoLoginVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial bool IsLoginUser { get; set; } = true;

    [ObservableProperty]
    public partial ObservableCollection<DateTimePoint> AllPoints { get; set; } = new();

    /// <summary>
    /// 是否加载列表
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoadCardItem { get; set; } = false;

    [ObservableProperty]
    public partial long CurrentPage { get; set; }

    [ObservableProperty]
    public partial long TotalPages { get; set; }

    partial void OnPageSizeChanged(long value)
    {
        if (value <= 0)
            PageSize = 10;
    }

    [RelayCommand]
    public async Task Loaded()
    {
        var users = await TryInvokeAsync(async () =>
            await CloudGameService.ConfigManager.GetUsersAsync(this.CTS.Token)
        );
        if (users.Item1 != 0 || users.Item2.Count == 0)
        {
            TipShow.ShowMessage("获取本地用户失败", Symbol.Clear);
            NoLoginVisibility = Visibility.Visible;
            this.LoadVisibility = Visibility.Collapsed;
            this.DataVisibility = Visibility.Collapsed;
            this.IsLoginUser = false;
            return;
        }
        this.Users = users.Item2;
        this.SelectedUser = Users[0];
        this.IsLoginUser = true;
    }

    [RelayCommand]
    public void ShowAnalysis()
    {
        ViewFactorys.ShowAnalysisRecord(this.SelectedUser).Show();
    }

    async partial void OnSelectedUserChanged(LoginData value)
    {
        if (value == null)
            return;
        var url = await TryInvokeAsync(async () =>
            await CloudGameService.GetRecordAsync(this.CTS.Token)
        );
        IsLoading = true;
        NoLoginVisibility = Visibility.Collapsed;
        this.LoadVisibility = Visibility.Visible;
        this.DataVisibility = Visibility.Collapsed;
        this.SelectRecordType = null;
        var result = await CloudGameService.OpenUserAsync(value);
        NoLoginVisibility = Visibility.Collapsed;
        this.LoadVisibility = Visibility.Collapsed;
        this.DataVisibility = Visibility.Visible;
        if (await SavePlayCardData())
        {
            this.SelectRecordType = RecordNavigationItems[0];
            if (!result.Item1)
            {
                TipShow.ShowMessage(result.Item2, Symbol.Clear);
                return;
            }
            this.IsLoginUser = true;
            this.PageSize = 10;
            this.CurrentPage = 1;
        }

        IsLoading = false;
    }

    async partial void OnSelectRecordTypeChanged(GameRecordNavigationItem value)
    {
        if (value == null)
            return;
        this.cacheItems = (List<RecordCardItemWrapper>)aLLcacheItems[value.Id];
        this.PageSize = 10;
        CurrentPage = 1;
        UpdatePageCount();
        LoadPageItems();
    }

    private void UpdatePageCount()
    {
        if (cacheItems == null || cacheItems.Count == 0)
        {
            TotalPages = 1;
            return;
        }

        var pageSize = (int)(PageSize > 0 ? PageSize : 10);
        TotalPages = (cacheItems.Count + pageSize - 1) / pageSize;
    }

    private void LoadPageItems()
    {
        ResourceItems.Clear();
        if (cacheItems == null || cacheItems.Count == 0)
            return;
        var pageSize = (int)(PageSize > 0 ? PageSize : 10);
        var pageIdx = (int)(CurrentPage <= 0 ? 1 : CurrentPage);
        var start = (pageIdx - 1) * pageSize;
        if (start >= cacheItems.Count)
            return;
        var page = cacheItems.Skip(start).Take(pageSize);
        foreach (var item in page)
            ResourceItems.Add(item);
    }

    [RelayCommand]
    public void NextPage()
    {
        if (cacheItems == null || cacheItems.Count == 0)
            return;

        UpdatePageCount();
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            LoadPageItems();
        }
    }

    [RelayCommand]
    public void PrevPage()
    {
        if (cacheItems == null || cacheItems.Count == 0)
            return;

        if (CurrentPage > 1)
        {
            CurrentPage--;
            LoadPageItems();
        }
    }

    [RelayCommand]
    public async Task ShowAdd()
    {
        await DialogManager.ShowWebGameDialogAsync();
    }

    public async Task<bool> SavePlayCardData()
    {
        try
        {
            this.aLLcacheItems.Clear();
            this.LoadVisibility = Visibility.Visible;
            this.DataVisibility = Visibility.Collapsed;
            var FiveGroup = await RecordHelper.GetFiveGroupAsync();
            var AllRole = await RecordHelper.GetAllRoleAsync();
            var AllWeapon = await RecordHelper.GetAllWeaponAsync();
            var StartRole = RecordHelper.FormatFiveRoleStar(FiveGroup);
            var StartWeapons = RecordHelper.FormatFiveWeaponeRoleStar(FiveGroup);
            var isLogin = await TryInvokeAsync(async () =>
                await CloudGameService.OpenUserAsync(this.SelectedUser)
            );
            if (isLogin.Result.Item1 == false)
            {
                TipShow.ShowMessage("登陆过期，请重新添加账号", Symbol.Clear);
                this.LoadVisibility = Visibility.Collapsed;
                this.DataVisibility = Visibility.Collapsed;
                return false;
            }
            var url = await TryInvokeAsync(async () =>
                await CloudGameService.GetRecordAsync(this.CTS.Token)
            );
            if (url.Result == null)
            {
                TipShow.ShowMessage("数据拉取失败！", Symbol.Clear);
                return false;
            }
            #region 读取抽卡记录
            if (url.Result.Data != null)
            {
                Dictionary<int, IList<RecordCardItemWrapper>> @param =
                    new Dictionary<int, IList<RecordCardItemWrapper>>();
                for (int i = 1; i < 10; i++)
                {
                    var player1 = await TryInvokeAsync(async () =>
                        await CloudGameService.GetGameRecordResource(
                            url.Item2.Data.RecordId,
                            url.Item2.Data.PlayerId.ToString(),
                            i,
                            this.CTS.Token
                        )
                    );
                    if (player1.Result == null)
                    {
                        TipShow.ShowMessage("数据拉取失败！", Symbol.Clear);
                        return false;
                    }
                    var WeaponsActivity = player1
                        .Result.Data.Select(x => new RecordCardItemWrapper(x))
                        .ToList();
                    param.Add(i, WeaponsActivity);
                }
                var cache = new RecordCacheDetily()
                {
                    Name = this.SelectedUser.Username,
                    Time = DateTime.Now,
                    RoleActivityItems = param[1],
                    WeaponsActivityItems = param[2],
                    RoleResidentItems = param[3],
                    WeaponsResidentItems = param[4],
                    BeginnerItems = param[5],
                    BeginnerChoiceItems = param[6],
                    GratitudeOrientationItems = param[7],
                    RoleJourneyItems = param[8],
                    WeaponJourneyItems = param[9],
                };
                var datas = MemoryPackSerializer.Serialize<RecordCacheDetily>(
                    cache,
                    new MemoryPackSerializerOptions() { StringEncoding = StringEncoding.Utf8 }
                );

                var result = await RecordHelper.MargeRecordAsync(AppSettings.RecordFolder, cache)!;
                TipShow.ShowMessage(
                    $"抽卡合并，数据总量{result.Item2},二进制大小{result.Item1 / 1024}KB",
                    Symbol.Accept
                );
                if (result.Item3 == null)
                    return false;
                this.aLLcacheItems.Add(1, result.Item3.RoleActivityItems ?? []);
                this.aLLcacheItems.Add(2, result.Item3.WeaponsActivityItems ?? []);
                this.aLLcacheItems.Add(3, result.Item3.RoleResidentItems ?? []);
                this.aLLcacheItems.Add(4, result.Item3.WeaponsResidentItems ?? []);
                this.aLLcacheItems.Add(5, result.Item3.BeginnerItems ?? []);
                this.aLLcacheItems.Add(6, result.Item3.BeginnerChoiceItems ?? []);
                this.aLLcacheItems.Add(7, result.Item3.GratitudeOrientationItems ?? []);
                this.aLLcacheItems.Add(8, result.Item3.RoleJourneyItems ?? []);
                this.aLLcacheItems.Add(9, result.Item3.WeaponJourneyItems ?? []);
                this.LoadVisibility = Visibility.Collapsed;
                this.DataVisibility = Visibility.Visible;
                this.IsLoadCardItem = true;
            }
            else
            {
                TipShow.ShowMessage($"{url.Message}", Symbol.Clear);
                this.LoadVisibility = Visibility.Collapsed;
                this.DataVisibility = Visibility.Collapsed;
                this.IsLoadCardItem = true;
            }
            return true;
            #endregion
        }
        catch (Exception ex)
        {
            TipShow.ShowMessage(ex.Message, Symbol.Clear);
            return false;
        }
    }

    [RelayCommand]
    public async Task SaveAsPlayCardData()
    {
        try
        {
            var savePath = await PickersService.GetFileSavePicker([".json"], "");
            if (savePath != null)
            {
                var cardDatas = await this.PlayerCardService.GetRecordAsync(this.SelectedUser.Username);
                var json = JsonSerializer.Serialize(cardDatas, RecordCacheDetilyContext.Default.RecordCacheDetily);
                if (File.Exists(savePath.Path))
                    File.Delete(savePath.Path);
                await File.WriteAllTextAsync(savePath.Path, json, encoding: Encoding.UTF8, this.CTS.Token);
                await TipShow.ShowMessageAsync("导出成功", Symbol.Accept);
            }
        }
        catch (Exception ex)
        {
            await TipShow.ShowMessageAsync($"导出失败{ex.Message}", Symbol.Clear);
        }
    }
}
