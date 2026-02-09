using Haiyu.ViewModel.GameViewModels;
using Haiyu.ViewModel.GameViewModels.Contracts;
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

namespace Haiyu.Pages.GamePages
{
    public sealed partial class WavesGamePage : Page,IPage
    {
        public WavesGamePage()
        {
            InitializeComponent();
            ViewModel =  Instance.Host.Services.GetRequiredService<WavesGameContextViewModel>();
        }

        public Type PageType => typeof(WavesGamePage);

        public WavesGameContextViewModel ViewModel { get; set;  }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.Bindings.StopTracking();
            this.ViewModel.Dispose();
            GC.Collect();
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }
    }
}
