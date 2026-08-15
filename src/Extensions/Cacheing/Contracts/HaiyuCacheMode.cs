namespace Cacheing.Contracts;

public enum HaiyuCacheMode
{
    /// <summary>
    /// 优先读取缓存，未命中时调用数据工厂并写入缓存。
    /// </summary>
    Default,

    /// <summary>
    /// 跳过已有缓存，重新调用数据工厂并覆盖缓存。
    /// </summary>
    Refresh,

    /// <summary>
    /// 仅读取缓存，未命中时返回默认值。
    /// </summary>
    CacheOnly,
}
