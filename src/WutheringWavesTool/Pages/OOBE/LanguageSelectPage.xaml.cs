using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Haiyu.Common.Contracts;
using Haiyu.ViewModel.OOBEViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Haiyu.Pages.OOBE
{
    public sealed partial class LanguageSelectPage : Page, IPage
    {
        public LanguageSelectPage()
        {
            InitializeComponent();
            this.ViewModel = Instance.Host.Services.GetRequiredService<LanguageSelectViewModel>();
        }

        public Type PageType => typeof(Page);

        public LanguageSelectViewModel? ViewModel { get; private set; }

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
    }
}
