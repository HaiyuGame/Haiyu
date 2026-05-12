using CommunityToolkit.Mvvm.ComponentModel;

namespace Haiyu.Plugin.Models;

public partial class DisplayVersionInfo:ObservableObject
{
    [ObservableProperty]
    public string Version { get; set; }

    [ObservableProperty]
    public string UpdateAt { get; set;  }

    [ObservableProperty]
    public long Size { get; set;  }

    [ObservableProperty]
    public string HelpLink { get; set;  }

    [ObservableProperty]
    public string DownloadLink { get; set; }

    /// <summary>
    /// 是否显示跳过
    /// </summary>
    public bool IsApply { get; set; }
}
