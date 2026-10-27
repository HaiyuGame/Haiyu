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
            SystemEventMessager.Publish(new() { Message = ex.Message, Delay = 30 });
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
