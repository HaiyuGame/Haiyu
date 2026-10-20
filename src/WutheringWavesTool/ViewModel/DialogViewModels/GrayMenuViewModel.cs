using System;
using System.Collections.Generic;
using System.Text;
using GrayExtension;
using Haiyu.Common.Contracts;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class GrayMenuViewModel : DialogViewModelBase
{
    public GrayMenuViewModel(DialogSession dialogSession, GrayOpenService grayOpenService)
        : base(dialogSession)
    {
        this.GrayService = grayOpenService;
    }

    public GrayOpenService GrayService { get; }

    [ObservableProperty]
    public partial string GrayValue { get; set; }


    [RelayCommand]
    public async Task Invoke()
    {
        if(uint.TryParse(GrayValue,out var id))
        {
            var isOpen = this.GrayService.GetOpen(id,false);
            await this.GrayService.SetDataAsync(id,!isOpen);
            this.GrayValue = "";
        }

    }
}
