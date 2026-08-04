using System;
using System.Collections.Generic;
using System.Text;

using Haiyu.Helpers;

namespace Haiyu.ViewModel.GameViewModels;

partial class WavesCloudGameViewModel
{
    [ObservableProperty]
    public partial string BottomText { get; set; } = LanguageService.GetString("ViewModel_Ready")!;

    [ObservableProperty]
    public partial string StartGameText { get; set; } = LanguageService.GetString("Display_MasukKeGame")!;

    
}
