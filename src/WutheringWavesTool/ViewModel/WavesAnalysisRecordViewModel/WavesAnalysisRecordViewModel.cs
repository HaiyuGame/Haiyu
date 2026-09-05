using Haiyu.Plugin.Common.LegacyMessageBox;
using Haiyu.Plugin.Contracts;
using Haiyu.Plugin.Models.Enums;
using MemoryPack;
using Waves.Api.Models.Enums;
using Waves.Api.Models.Record;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.Models.CloudGame;
using Waves.Core.Services;
using Windows.Storage;
using static System.Net.WebRequestMethods;

namespace Haiyu.ViewModel;

/// <summary>
/// 鸣潮抽卡分析
/// </summary>
public sealed partial class WavesAnalysisRecordViewModel : WindowViewModelBase
{
    public readonly IKuroCloudGameContext CloudGameContext;
    public CloudGameLoginSession Session { get; set; }
    public IWavesPlayerCardCacheServices Cache { get; }
    public IPickersService PickersService { get; }
    public IWavesCloudGameService WavesCloudGameService { get; }
    public FiveGroupModel FiveGroup { get; private set; }
    public List<CommunityRoleData> AllRole { get; private set; }
    public List<CommunityWeaponData> AllWeapon { get; private set; }
    private WavesAnalysisPlayerCard Cards { get; set; }

    public WavesAnalysisRecordViewModel(
        [FromKeyedServices(nameof(KuroCloudGameContext))] IKuroCloudGameContext cloudGameContext,
        IWavesPlayerCardCacheServices cache,
        IPickersService pickersService,
        IWavesCloudGameService wavesCloudGameService
    )
    {
        InitializeCharts();
        this.CloudGameContext = cloudGameContext;
        Cache = cache;
        PickersService = pickersService;
        WavesCloudGameService = wavesCloudGameService;
    }

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [RelayCommand]
    async Task Loaded()
    {
        try
        {
            await LoadDataAsync();
            IsLoading = true;
            await InitAnalysis();
            await AnalysisStarAsync();
            IsLoading = false;
        }
        catch (Exception ex)
        {
            Instance
                .Host.Services.GetRequiredService<SystemEventPublisher>()
                .Publish(new() { Message = ex.Message, Delay = 30 });
        }
    }

    private async Task InitAnalysis()
    {
        this.FiveGroup = await RecordHelper.GetFiveGroupAsync(this.CTS.Token) ?? new();
        this.AllRole = await RecordHelper.GetAllRoleAsync(this.CTS.Token) ?? new();
        this.AllWeapon = await RecordHelper.GetAllWeaponAsync(this.CTS.Token) ?? new();
        InitNavItems();
    }

    private async Task LoadDataAsync()
    {
        await MargeNewRecordPlayerAsync();
    }

    async Task MargeNewRecordPlayerAsync()
    {
        var newRecord = await this.WavesCloudGameService.GetRecordAsync(
            this.Session,
            this.CTS.Token
        );
        if (newRecord == null || newRecord.Data == null)
        {
            return;
        }
        WavesAnalysisPlayerCard cards = new()
        {
            Items = new List<WavesAnalysisPlayerCardItem>(),
            LastUpdater = DateTime.Now,
            SessionId = this.Session.OrginData.Username + this.Session.OrginData.Sdkuserid,
        };
        foreach (var item in CardPoolTypeValues.All)
        {
            var resources = await this.WavesCloudGameService.GetGameRecordResource(
                Session,
                newRecord.Data.RecordId,
                newRecord.Data.PlayerId.ToString(),
                (int)item
            );
            if (resources?.Data != null)
            {
                cards.Items.Add(
                    new WavesAnalysisPlayerCardItem()
                    {
                        PoolType = (int)item,
                        Resource = resources.Data.Select(x => new RecordCardItemWrapper(x)),
                    }
                );
            }
        }
        this.Cards = await Cache.SaveAsync(cards);
    }

    [RelayCommand]
    async Task ImportFolder()
    {
        try
        {
            //TODO 后续修改
            //var picker = await PickersService.GetFolderPicker();
            //
            //if (picker == null)
            //    return;
            //
            //var files = picker.Path;
            //await ImportDefaultFolderAsync(files);
        }
        catch (Exception ex)
        {
            LegacyMessageBox.ShowError(
                LanguageService.FormatByText(
                    LanguageService.GetStringByText("导入失败：{0}"),
                    ex.Message
                )
            );
        }
    }

    [RelayCommand]
    public async Task ImportDefaultFolderAsync(string folderPath = null)
    {
        if (folderPath == null)
        {
            folderPath = Waves.Settings.AppSettings.RecordFolder;
        }
        var jsonFiles = Directory.GetFiles(folderPath, "*.json").ToList();
        if (jsonFiles.Count == 0)
        {
            LegacyMessageBox.ShowError(
                LanguageService.GetStringByText("导入失败：文件夹中没有找到.json文件")
            );
            return;
        }

        var mergedCard = new WavesAnalysisPlayerCard()
        {
            Items = new List<WavesAnalysisPlayerCardItem>(),
            LastUpdater = DateTime.Now,
            SessionId =
                this.Session?.OrginData.Username + this.Session?.OrginData.Sdkuserid ?? "import",
        };
        var loadedNames = new List<string>();

        foreach (var file in jsonFiles)
        {
            try
            {
                var cache = MemoryPackSerializer.Deserialize<RecordCacheDetily>(
                    await System.IO.File.ReadAllBytesAsync(file),
                    new MemoryPackSerializerOptions() { StringEncoding = StringEncoding.Utf8 }
                );
                if (cache == null)
                    continue;
                ArgumentNullException.ThrowIfNull(this.Session);
                if (cache.Name != this.Session.OrginData.Username)
                {
                    continue;
                }
                loadedNames.Add(cache.Name);

                void AddPool(int poolType, IList<RecordCardItemWrapper>? resources)
                {
                    if (resources != null && resources.Count > 0)
                    {
                        mergedCard.Items.Add(
                            new WavesAnalysisPlayerCardItem()
                            {
                                PoolType = poolType,
                                Resource = resources,
                            }
                        );
                    }
                }

                AddPool((int)CardPoolType.RoleActivity, cache.RoleActivityItems);
                AddPool((int)CardPoolType.WeaponsActivity, cache.WeaponsActivityItems);
                AddPool((int)CardPoolType.RoleResident, cache.RoleResidentItems);
                AddPool((int)CardPoolType.WeaponsResident, cache.WeaponsResidentItems);
                AddPool((int)CardPoolType.Beginner, cache.BeginnerItems);
                AddPool((int)CardPoolType.BeginnerChoice, cache.BeginnerChoiceItems);
                AddPool((int)CardPoolType.GratitudeOrientation, cache.GratitudeOrientationItems);
                AddPool((int)CardPoolType.CharacterNovice, cache.RoleJourneyItems);
                AddPool((int)CardPoolType.WeaponNovice, cache.WeaponJourneyItems);
            }
            catch
            {
                continue;
            }
        }

        if (loadedNames.Count == 0)
        {
            LegacyMessageBox.ShowError(
                LanguageService.GetStringByText("导入失败：未读取到有效的抽卡记录")
            );
            return;
        }

        this.Cards = await Cache.SaveAsync(mergedCard);
        var names = string.Join("、", loadedNames);
        LegacyMessageBox.ShowInformation(
            LanguageService.FormatByText(
                LanguageService.GetStringByText("成功导入 {0} 个账号的数据：{1}"),
                loadedNames.Count,
                names
            )
        );
        IsLoading = true;
        await InitAnalysis();
        await AnalysisStarAsync();
        IsLoading = false;
    }

    internal void SetSessionAsync(CloudGameLoginSession session)
    {
        this.Session = session;
    }

}
