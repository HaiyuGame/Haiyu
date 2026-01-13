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

namespace Haiyu.Pages.Dialogs;
public sealed partial class LocalUserManagerDialog : ContentDialog,IDialog
{
    public LocalUserManagerDialog()
    {
        InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<LocalUserManagerViewModel>();
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public LocalUserManagerViewModel ViewModel { get; }

    public void SetData(object data)
    {
    }
}