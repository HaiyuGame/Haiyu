using System;
using System.Collections.Generic;
using System.Text;
using Haiyu.ViewModel.OOBEViewModels;

namespace Haiyu.ViewModel;

partial class SettingViewModel
{
    [ObservableProperty]
    public partial LanguageSelectViewModel LanguageSelectViewModel { get; set; }
}
