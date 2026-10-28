using Haiyu.Common.Contracts;
using Haiyu.Models.Dialogs;
using Haiyu.ViewModel.DialogViewModels.WavesRecords;

namespace Haiyu.Pages.Dialogs.WavesRecords;

public sealed partial class MergeRecordDialog : ContentDialog, IDialog
{
    public MergeRecordDialog(MergeRecordViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
        Closing += (_, args) => args.Cancel = ViewModel.IsBusy;
    }

    public MergeRecordViewModel ViewModel { get; }

    public static Visibility ErrorVisibility(string message) =>
        string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;

    public static bool IsIdle(bool isBusy) => !isBusy;

    public void SetData(object data)
    {
        if (data is WavesRecordMergeRequest request)
            ViewModel.Initialize(request);
    }
}
