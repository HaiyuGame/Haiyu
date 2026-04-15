using Haiyu.Models.Dialogs;
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
using Waves.Core.Models.Enums;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Haiyu.Pages.Dialogs;


public sealed partial class UpdateGameDialogV2 : ContentDialog,
     IResultDialog<UpdateGameResult>
{
    public UpdateGameDialogV2()
    {
        InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<UpdateGameViewModelV2>();
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public UpdateGameViewModelV2 ViewModel { get; }

    public UpdateGameResult? GetResult()
    {
        return ViewModel.GameResult();
    }

    public void SetData(object data)
    {
        if (data is Tuple<string, UpdateGameType> tuple)
        {
            if (Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(tuple.Item1) is IGameContextV2 context)
            {
                this.ViewModel.SetData(context, tuple.Item2);
            }
        }

    }
}
