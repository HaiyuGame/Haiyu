
using Haiyu.Helpers;
using Haiyu.Models.Wrapper.Wiki;
using Waves.Api.Models.GameWikiiClient;

namespace Haiyu.ViewModel.WikiViewModels;

public partial class PunishWikiViewModel : WikiViewModelBase
{
    public PunishWikiViewModel() { }

    [ObservableProperty]
    public partial ObservableCollection<HotContentSideWrapper> Sides { get; set; }

    [ObservableProperty]
    public partial PunishEveryweekContentWrapper EveryWeekContent { get; set; }

    [ObservableProperty]
    public partial PunishBannerWrapper BannerListContentWrapper { get; set; }
    [RelayCommand]
    async Task Loaded()
    {
        var wikiPage = await TryInvokeAsync(async () =>
            await this.GameWikiClient.GetHomePageAsync(WikiType.BGR, this.CTS.Token)
        );
        if (wikiPage.Code == 0 || (wikiPage.Result != null && wikiPage.Result.Data.ContentJson.Shortcuts != null))
        {
            Sides = GameWikiClient.GetEventData(wikiPage.Result).Format() ?? [];
            if (EveryWeekContent == null) EveryWeekContent = new();
            if (BannerListContentWrapper == null) BannerListContentWrapper = new();

            var sideModule = wikiPage.Result.Data.ContentJson.SideModules;
            var mainModule = wikiPage.Result.Data.ContentJson.MainModules;
            EveryWeekContent.InitWeekContent(sideModule, mainModule);
            BannerListContentWrapper.InitBanner(sideModule);
        }
        else
        {
            TipShow.ShowMessage($"获取数据失败，请检查网络或重启应用", Symbol.Clear);
        }
    }

    private void TestFunction((int code, WikiHomeModel result, string? msg) wikiPage)
    {
        using (StreamWriter sw = new StreamWriter("F:\\Code_Project\\haiyu\\Dev\\Txt.txt"))
        {
            foreach (var value in wikiPage.result.Data.ContentJson.SideModules)
            {
                if (value == null) return;
                if (value.Title.Contains("池"))
                {
                    sw.WriteLine(value.Title);
                    sw.WriteLine(value.Content);
                    sw.WriteLine("\n");
                }
                continue;

            }
        }
    }
    public override void Dispose()
    {
        Sides?.Clear();
        EveryWeekContent?.Dispose();
        BannerListContentWrapper?.Dispose();
        if (EveryWeekContent != null) EveryWeekContent = null;
        if (BannerListContentWrapper != null) BannerListContentWrapper = null;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        base.Dispose();
    }
}

