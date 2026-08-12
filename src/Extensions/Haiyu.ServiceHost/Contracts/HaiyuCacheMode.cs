namespace Haiyu.ServiceHost.Contracts;

public enum HaiyuCacheMode
{
    /// <summary>
    /// 默认行为，缓存
    /// </summary>
    Default,

    /// <summary>
    /// 刷新行为，不缓存
    /// </summary>
    Refresh,
    
    /// <summary>
    /// 只读缓存
    /// </summary>
    CacheOnly,
}
