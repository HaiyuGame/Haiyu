using System;
using System.Collections.Generic;
using System.Text;
using Haiyu.Common.Contracts;
using Haiyu.Models.Dialogs;

namespace Haiyu.ViewModel.DialogViewModels.WavesRecords;

public sealed partial class RenameRecordViewModel : DialogViewModelBase
{
    public RenameRecordViewModel(
        DialogSession dialogSession,
        Haiyu.Services.WavesRecordManager wavesRecordManager
    )
        : base(dialogSession)
    {
        WavesRecordManager = wavesRecordManager;
    }

    public WavesRecordManager WavesRecordManager { get; }
    public WavesAnalysisPlayerCard? CurrentSession { get; private set; }

    [ObservableProperty]
    public partial string NewName { get; set; }

    public async Task Initialize(WavesRecordMergeRequest request)
    {
        var session = await this.WavesRecordManager.GetLocalWavesRecordAsync(request.SessionId);
        ArgumentNullException.ThrowIfNull(session, "获取抽卡档案失效");
        this.CurrentSession = session;
        this.NewName = request.RecordName;
    }

    [RelayCommand]
    async Task SaveRecord()
    {
        ArgumentNullException.ThrowIfNull(this.CurrentSession, "获取抽卡档案失效，保存失败");
        this.CurrentSession.RecordName = this.NewName;
        await this.WavesRecordManager.CacheService.SaveAsync(this.CurrentSession);
        await this.CloseAsync();
    }
}
