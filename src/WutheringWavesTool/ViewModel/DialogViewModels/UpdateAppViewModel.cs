using Haiyu.Plugin.Contracts;
using Haiyu.Plugin.Models;
using Haiyu.Services.DialogServices;
using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class UpdateAppViewModel : DialogViewModelBase
{
    private DisplayVersionInfo _info;

    [ObservableProperty]
    public partial string Version { get; set; }

    [ObservableProperty]
    public partial string Size { get; set; }

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial Visibility SkipVisiblity { get; set; }

    public IUpdateService UpdateService { get; }

    public UpdateAppViewModel([FromKeyedServices(nameof(MainDialogService))] IDialogManager dialogManager) : base(dialogManager)
    {
        if(AppSettings.UpdateType == "Github")
        {
            UpdateService = Instance.Host.Services.GetRequiredKeyedService<IUpdateService>("GitHub");
        }
         else
        {
            //UpdateService = AppContext.Host.Services.GetRequiredService<IUpdateService>("Mirror");
        }
    }


    internal void SetInfo(DisplayVersionInfo info)
    {
        this._info = info;
        this.Version = _info.Version;
        this.Size = $"{ByteConversion.BytesToMegabytes(_info.Size, 2)}Mib";
        this.SkipVisiblity =  info.IsApply == true? SkipVisiblity = Visibility.Collapsed : SkipVisiblity = Visibility.Visible;
    }

    [RelayCommand]
    void SkipAppUpdate()
    {
        AppSettings.SkipAppVersion = _info.Version;
        this.Close();
    }

    [RelayCommand]
    async Task DownloadAppUpdate()
    {
        IProgress<double> progress = new Progress<double>((s) =>
        {
            Progress = s;
        });
        var path =  await UpdateService.DownloadProgramInfoAsync(progress, this.CTS.Token);
        ProcessStartInfo info = new ProcessStartInfo(path);
        info.Verb = "runas";
        info.UseShellExecute = true;
        Process.Start(info);
        Environment.Exit(0);
    }
}
