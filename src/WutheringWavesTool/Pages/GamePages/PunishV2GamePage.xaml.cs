using Haiyu.Common.Contracts;
using Haiyu.ViewModel.GameViewModels.GameContexts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Waves.Core.GameContext.ContextsV2;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Haiyu.Pages.GamePages;

public sealed partial class PunishV2GamePage : Page,IPage
{
    public Type PageType => typeof(PunishV2GamePage);

    public PunishV2GamePage()
    {
        InitializeComponent();
        ViewModel = Instance.Host.Services.GetRequiredService<PunishV2GameContextViewModel>();
    }


    public PunishV2GameContextViewModel? ViewModel { get; set; }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        try
        {
            this.Bindings.StopTracking();
            this.ViewModel?.Dispose();
        }
        finally
        {
            this.ViewModel = null;
            base.OnNavigatedFrom(e);
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        switcher.Switch();
        if(switcher.CurrentIndex == 1)
        {
            filpViewAutoPlay.IsPlay = true;
        }
        else
        {
            filpViewAutoPlay.IsPlay = false;
        }
    }
}
