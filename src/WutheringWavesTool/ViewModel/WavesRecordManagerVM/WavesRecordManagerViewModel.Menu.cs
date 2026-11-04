using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.ViewModel.WavesRecordManagerVM
{
    partial class WavesRecordManagerViewModel
    {
        [RelayCommand]
        async Task ShowRenameDialog()
        {
            await WinSession.Context.DialogManager.ShowWavesRenameAsync(
                new Models.Dialogs.WavesRecordMergeRequest(
                    this.SelectFile.SessionId,
                    this.SelectFile.DisplayName
                )
            );
            await this.Loaded();
        }

        [RelayCommand]
        async Task ShowAddGameContextLog()
        {
            await WinSession.Context.DialogManager.ShowWavesRecordCreateForGameAsync(null);
            await this.Loaded();
        }
    }
}
