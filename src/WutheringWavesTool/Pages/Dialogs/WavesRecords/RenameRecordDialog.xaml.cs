using Haiyu.Common.Contracts;
using Haiyu.Models.Dialogs;
using Haiyu.ViewModel.DialogViewModels.WavesRecords;

namespace Haiyu.Pages.Dialogs.WavesRecords;

public sealed partial class RenameRecordDialog : ContentDialog, IDialog
{
    public RenameRecordDialog(RenameRecordViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public RenameRecordViewModel ViewModel { get; }



    public async void SetData(object data)
    {
        if (data is WavesRecordMergeRequest request)
        {
            await this.ViewModel.Initialize(request);
        }
    }
}
