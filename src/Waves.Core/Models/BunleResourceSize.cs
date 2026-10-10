using Waves.Core.Models.Messanger;

namespace Waves.Core.Models;

public sealed partial class BunleResourceSize : ObservableObject
{
    public BunleResourceSize(long size)
    {
        Size = size;
        BunleSize = $"{(Size / (1024.0 * 1024.0 * 1024.0)):F2} GB";
    }

    [ObservableProperty]
    public partial bool IsSelect { get; set; }

    [ObservableProperty]
    public partial string BunleName { get; set; }

    [ObservableProperty]
    public partial string BunleSize { get; set; }

    [ObservableProperty]
    public partial string Version { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<string> GPUS { get; set; }

    [RelayCommand]
    void SendDelete()
    {
        WeakReferenceMessenger.Default.Send<DeleteBunleGameResourceMessager>(new(this.BunleName));
    }

    [ObservableProperty]
    public partial string ResourceCommand { get; set; }
    public long Size { get; }
    /// <summary>
    /// 删除按钮显示
    /// </summary>
    [ObservableProperty]
    public partial bool Select { get; set; }
}
