using Haiyu.Common.Contracts;
using Haiyu.Models.Dialogs;
using Haiyu.ViewModel.DialogViewModels.WavesRecords;

namespace Haiyu.Pages.Dialogs.WavesRecords;

public sealed partial class WutheringWavesToolImportDialog : ContentDialog, IDialog
{
    public WutheringWavesToolImportDialog(MergeRecordViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
        Closing += (_, args) => args.Cancel = ViewModel.IsBusy;
    }

    // 共用预览、重合度及合并状态，格式转换在读取服务中完成。
    public MergeRecordViewModel ViewModel { get; }

    public void SetData(object data)
    {
        if (data is WavesRecordMergeRequest request)
            ViewModel.Initialize(request, isExcel: true);
    }
}
