using Haiyu.Common.Contracts;

namespace Haiyu.ViewModel.WavesRecordManager;

public partial class WavesRecordManagerViewModel : WindowViewModelBase
{
    public WavesRecordManagerViewModel(
        WindowSession session,
        Haiyu.Services.WavesRecordManager wavesRecordManager
    )
    {
        this.WinSession = session;
        WavesRecordManager = wavesRecordManager;
    }

    public WindowSession WinSession { get; internal set; }
    public Services.WavesRecordManager WavesRecordManager { get; }

    [ObservableProperty]
    public partial ObservableCollection<WavesRecordDisplayWrapper> LocalFiles { get; set; }

    [ObservableProperty]
    public partial WavesRecordDisplayWrapper SelectFile { get; set; }

    private List<WavesAnalysisPlayerCard> playerCards = new();

    /// <summary>
    /// 利用老的分析面板展示
    /// </summary>
    [ObservableProperty]
    public partial WavesRecordAnalysisViewModel AnalysisViewModel { get; set; } = new();

    async partial void OnSelectFileChanged(WavesRecordDisplayWrapper value)
    {
        if (value == null)
            return;
        try
        {
            await RefreshViewModelAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await this.WinSession.Context.TipShow.ShowMessageAsync(ex.Message, Symbol.Clear);
        }
    }

    private async Task RefreshViewModelAsync()
    {
        var card = playerCards.FirstOrDefault(x => x.SessionId == SelectFile?.SessionId);
        if (card != null)
            await AnalysisViewModel.InitializeAsync(card);
    }

    protected override void OnDisposing()
    {
        AnalysisViewModel.Dispose();
        base.OnDisposing();
    }

    [RelayCommand]
    Task MergeLocalHaiyuV2RecordAsync() => ImportIntoSelectedRecordAsync(false);

    [RelayCommand]
    Task ImportWutheringWavesToolAsync() => ImportIntoSelectedRecordAsync(true);

    [RelayCommand]
    private async Task ExportAllRecordsAsync()
    {
        try
        {
            var cards = await WavesRecordManager.GetLocalWavesRecordsAsync(CTS.Token);
            if (cards.Count == 0)
            {
                await this.WinSession.Context.TipShow.ShowMessageAsync(
                    "暂无本地档案可导出",
                    Symbol.Clear
                );
                return;
            }

            var file = await WinSession.Context.PickersService.GetFileSavePicker(
                [".xlsx"],
                $"Haiyu-全部抽卡记录-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx"
            );
            if (file == null)
                return;

            await this.WinSession.Context.TipShow.ShowMessageAsync(
                "正在导出全部抽卡记录",
                Symbol.Message
            );
            var result = await WavesRecordManager.ExportAllRecordsAsync(file.Path, CTS.Token);

            await this.WinSession.Context.TipShow.ShowMessageAsync(
                $"已导出 {result.Archives} 个档案、{result.Records} 条抽卡记录至：{file.Path}",
                Symbol.Message
            );
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await this.WinSession.Context.TipShow.ShowMessageAsync(
                $"导出失败：{ex.Message}",
                Symbol.Clear
            );
        }
    }

    private async Task ImportIntoSelectedRecordAsync(bool isExcel)
    {
        var selected = SelectFile;
        var targetSessionId = selected?.SessionId;
        if (string.IsNullOrWhiteSpace(targetSessionId))
        {
            await this.WinSession.Context.TipShow.ShowMessageAsync(
                $"请先在左侧选择要合并到的目标档案。",
                Symbol.Clear
            );
            return;
        }
        try
        {
            var request = new Haiyu.Models.Dialogs.WavesRecordMergeRequest(
                targetSessionId,
                selected!.DisplayName,
                WinSession.Context.GetWindow().GetWindowHandle()
            );
            var merged = isExcel
                ? await WinSession.Context.DialogManager.ShowWutheringWavesToolImportAsync(request)
                : await WinSession.Context.DialogManager.ShowWavesRecordMergeAsync(request);
            if (!merged)
                return;
            playerCards = await WavesRecordManager.GetLocalWavesRecordsAsync();
            await RefreshViewModelAsync();
            await this.WinSession.Context.TipShow.ShowMessageAsync(
                $"抽卡记录已合并，原文件已保留。",
                Symbol.Clear
            );
        }
        catch (Exception ex)
        {
            await this.WinSession.Context.TipShow.ShowMessageAsync(
                $"合并抽卡记录失败：{ex.Message}",
                Symbol.Clear
            );
        }
    }

    [RelayCommand]
    async Task Loaded()
    {
        playerCards = await WavesRecordManager.GetLocalWavesRecordsAsync();
        LocalFiles = playerCards
            .Select(x => new WavesRecordDisplayWrapper()
            {
                DisplayName = x.RecordName,
                SessionId = x.SessionId,
            })
            .ToObservableCollection();
        SelectFile = LocalFiles.FirstOrDefault();
    }
}

public class WavesRecordDisplayWrapper
{
    public string DisplayName { get; set; }

    public string SessionId { get; set; }
}
