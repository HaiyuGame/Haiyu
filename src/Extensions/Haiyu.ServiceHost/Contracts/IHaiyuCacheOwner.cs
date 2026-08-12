namespace Haiyu.ServiceHost.Contracts;

/// <summary>
/// Exposes the shared cache service to code emitted by <c>HaiyuCacheGenerator</c>.
/// </summary>
public interface IHaiyuCacheOwner
{
    IHaiyuMemoryCacheService CacheService { get; }
}
