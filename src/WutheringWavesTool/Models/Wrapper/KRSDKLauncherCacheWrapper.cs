using Waves.Api.Models.Launcher;

namespace Haiyu.Models.Wrapper;

public sealed partial class KRSDKLauncherCacheWrapper:ObservableObject
{
    public KRSDKLauncherCacheWrapper(KRSDKLauncherCache cache,QueryPlayerItem playerItem)
    {
        Cache = cache;
        this.PlayerItem = playerItem;
    }

    public KRSDKLauncherCache Cache { get; }

    public QueryPlayerItem PlayerItem { get; }

    [ObservableProperty]
    public partial bool IsSelect { get; set; }

    /// <summary>
    /// 获取当前账户唯一定位符号
    /// </summary>
    public string GetKey => $"{Cache.Username}_{PlayerItem.ServerName}_{PlayerItem.RoleName}";
}
