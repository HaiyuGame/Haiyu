using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Haiyu.Mobile.Views;

namespace Haiyu.Mobile.ViewModels;

public sealed partial class UserManagerViewModel:ObservableRecipient
{
    public UserManagerViewModel()
    {
        
    }


    [RelayCommand]
    async Task CreateKuroUser()
    {
        await Shell.Current.GoToAsync(nameof(AddKuroUserPage),true);
    }
}
