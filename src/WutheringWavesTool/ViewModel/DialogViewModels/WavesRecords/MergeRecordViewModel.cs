using Haiyu.Common.Contracts;
using Haiyu.Models.Dialogs;
using Haiyu.Pickers;

namespace Haiyu.ViewModel.DialogViewModels.WavesRecords;

public partial class MergeRecordViewModel : DialogViewModelBase
{
    private readonly Services.WavesRecordManager _manager;
    private readonly IPickersService _pickers;
    private string _targetSessionId = "";
    private WavesAnalysisPlayerCard? _imported;
    private bool _isExcel;

    public MergeRecordViewModel(DialogSession session, Services.WavesRecordManager manager, IPickersService pickers)
        : base(session)
    {
        _manager = manager;
        _pickers = pickers;
    }

    [ObservableProperty]
    public partial string RecordName { get; set; } = "";
    [ObservableProperty]
    public partial string FilePath { get; set; } = "尚未选择文件";
    [ObservableProperty]
    public partial double OverlapPercent { get; set; }
    [ObservableProperty]
    public partial string OverlapLabel { get; set; } = "待比较";
    [ObservableProperty]
    public partial string TotalLabel { get; set; } = "—";
    [ObservableProperty]
    public partial string DuplicateLabel { get; set; } = "—";
    [ObservableProperty]
    public partial string AddedLabel { get; set; } = "—";
    [ObservableProperty]
    public partial string Summary { get; set; } = "选择旧版文件后显示重合度；确认合并前不会修改档案。";
    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = "";
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public void Initialize(WavesRecordMergeRequest request, bool isExcel = false)
    {
        _isExcel = isExcel;
        if (isExcel)
            Summary = "选择 Excel 文件后预览重合度，确认前不会修改档案。";
        _targetSessionId = request.SessionId;
        RecordName = request.RecordName;
        _pickers.InitWindow(request.WindowHandle);
    }

    private bool CanSelectFile() => !IsBusy;
    private bool CanMerge() => !IsBusy && _imported != null;

    partial void OnIsBusyChanged(bool value)
    {
        SelectFileCommand.NotifyCanExecuteChanged();
        MergeCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSelectFile))]
    private async Task SelectFileAsync()
    {
        IsBusy = true;
        ErrorMessage = "";
        try
        {
            var file = await _pickers.GetFileOpenPicker(_isExcel ? [".xlsx"] : [".record"]);
            if (file == null)
                return;
            _imported = null;
            OverlapPercent = 0;
            OverlapLabel = "比较中";
            TotalLabel = DuplicateLabel = AddedLabel = "—";
            Summary = "正在读取并比较抽卡记录…";
            FilePath = file.Path;
            var preview = _isExcel
                ? await _manager.PreviewExcelImportAsync(file.Path, _targetSessionId, CTS.Token)
                : await _manager.PreviewRecordImportAsync(file.Path, _targetSessionId);
            _imported = preview.Card;
            OverlapPercent = preview.Percent;
            OverlapLabel = $"{preview.Percent:F1}%";
            TotalLabel = preview.Total.ToString("N0");
            DuplicateLabel = preview.Duplicates.ToString("N0");
            AddedLabel = (preview.Total - preview.Duplicates).ToString("N0");
            Summary = "确认后合并记录，保留目标档案信息和旧文件。";
        }
        catch (Exception ex)
        {
            _imported = null;
            OverlapLabel = "待比较";
            Summary = _isExcel ? "请重新选择 WutheringWavesTool 导出的 .xlsx 文件。" : "请重新选择有效的 .record 文件。";
            ErrorMessage = $"读取失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanMerge))]
    private async Task MergeAsync()
    {
        if (_imported == null)
            return;
        IsBusy = true;
        ErrorMessage = "";
        var succeeded = false;
        try
        {
            // 使用预览时读入的数据，避免源文件变化导致预览与实际导入不一致。
            await _manager.MergeRecordCardsAsync(_imported, _targetSessionId);
            succeeded = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"合并失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
        if (succeeded)
            await CloseAsync(true);
    }
}
