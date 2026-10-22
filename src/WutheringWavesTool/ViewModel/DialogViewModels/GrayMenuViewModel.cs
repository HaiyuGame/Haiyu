using System;
using System.Collections.Generic;
using System.Text;
using GrayExtension;
using Haiyu.Common.Contracts;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Core;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class GrayMenuViewModel : DialogViewModelBase
{
    public GrayMenuViewModel(DialogSession dialogSession, GrayOpenService grayOpenService)
        : base(dialogSession)
    {
        this.GrayService = grayOpenService;
        this.GrayValue = GrayConfigItem.GetItems;
        foreach (var item in this.GrayValue)
        {
            var isOpen = this.GrayService.GetOpen((uint) item.Id, false);
            item.IsOpen = isOpen;
        }
    }

    public GrayOpenService GrayService { get; }

    [ObservableProperty]
    public partial ObservableCollection<GrayConfigItem> GrayValue { get; set; }


    [RelayCommand]
    public async Task Invoke()
    {
        foreach (var item in this.GrayValue)
        {
            await this.GrayService.SetDataAsync((uint)item.Id, item.IsOpen);
        }

        AppRestartFailureReason restartError = AppInstance.Restart(null);
    }
}
