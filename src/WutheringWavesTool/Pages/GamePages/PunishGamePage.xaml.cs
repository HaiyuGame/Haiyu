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
using Windows.Foundation;
using Windows.Foundation.Collections;
namespace Haiyu.Pages.GamePages;

public sealed partial class PunishGamePage : Page,IPage
{
    public Type PageType => typeof(PunishGamePage);

    public PunishGamePage()
    {
        InitializeComponent();
        ViewModel = Instance.Host.Services.GetRequiredService<PunishGameContextViewModel>();
    }


    public PunishGameContextViewModel ViewModel { get; set; }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        this.Bindings.StopTracking();
        this.ViewModel.Dispose();
        base.OnNavigatedFrom(e);
        GC.Collect();
    }
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        switcher.Switch();
    }
}
