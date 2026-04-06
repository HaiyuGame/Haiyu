using CommunityToolkit.Mvvm.ComponentModel;

namespace Waves.Core.Models;

public sealed partial class DownloadActiveFileItem:ObservableObject
{
    [ObservableProperty]
    public partial string FileName { get; set; }


    [ObservableProperty]
    public partial double CurrentSize { get; set; }


    [ObservableProperty]
    public partial double TotalSize { get; set; }

    [ObservableProperty]
    public partial double Progress { get; set; }

    partial void OnTotalSizeChanged(double value)
    {
        ChangedProgress();
    }

    private void ChangedProgress()
    {
        if(this.TotalSize == 0 || this.CurrentSize == 0)
        {
            this.Progress = 0;
        }
        else
        {
            this.Progress = Math.Round((double)this.CurrentSize / this.TotalSize * 100,2);
        }
    }

    partial void OnCurrentSizeChanged(double value)
    {
        ChangedProgress();
    }
}


public sealed partial class DownloadSetupItem:ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public bool IsOK { get; set; }
}