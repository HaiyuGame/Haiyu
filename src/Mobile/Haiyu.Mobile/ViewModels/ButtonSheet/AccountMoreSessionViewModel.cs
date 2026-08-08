using System;
using System.Collections.Generic;
using System.Text;
using Haiyu.Mobile.Common;
using Plugin.Maui.BottomSheet.Navigation;

namespace Haiyu.Mobile.ViewModels.ButtonSheet;

public partial class AccountMoreSessionViewModel:ButtonSheetViewModel
{
    public string UserId { get; private set; }

    public override void OnNavigatedTo(IBottomSheetNavigationParameters parameters)
    {
        if(parameters.TryGetValue("userId",out var value) && value is string userId)
        {
            this.UserId = userId;
        }
    }
}
