using CommunityToolkit.Mvvm.ComponentModel;
using CounterMonitor;
using System;
using System.Collections.Generic;
using System.Text;


namespace WinUIUnitTestApp1.ViewModels;

public sealed partial class BlackPageViewModel:ObservableObject
{
    FPSCounter fps = new FPSCounter();
    public BlackPageViewModel()
    {
        fps.Start();
        fps.FpsOutputChanged += Fps_FpsOutputChanged;
    }

    [ObservableProperty]
    public partial string TipMessage { get; set; }
    
    private void Fps_FpsOutputChanged(object sender, CounterMonitor.Models.FpsOutput outPut)
    {
        UnitTestApp._window.DispatcherQueue.TryEnqueue(() =>
        {
            this.TipMessage = $"Name:{outPut.Name},FPS{outPut.FPS}";
        });
    }
}
