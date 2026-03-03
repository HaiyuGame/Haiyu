using Waves.Api.Models.Launcher;

namespace Haiyu.Models.Wrapper;

public sealed partial class KRSDKLauncherCacheWrapper:ObservableObject
{
    public KRSDKLauncherCacheWrapper(KRSDKLauncherCache cache,string localSelect)
    {
        Cache = cache;
        if(localSelect == Cache.Username)
        {
            this.IsSelect = true;
        }
    }

    public KRSDKLauncherCache Cache { get; }

    [ObservableProperty]
    public partial bool IsSelect { get; set; }
}
