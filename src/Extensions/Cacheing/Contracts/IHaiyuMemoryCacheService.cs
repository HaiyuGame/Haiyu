namespace Cacheing.Contracts;

public interface IHaiyuMemoryCacheService
{
    Task<T?> GetOrCreateAsync<T>(
        string targetName,
        string key,
        string instanceKey,
        TimeSpan expiration,
        Func<CancellationToken, Task<T?>> factory,
        HaiyuCacheMode mode = HaiyuCacheMode.Default,
        CancellationToken cancellationToken = default
    );

    void Set<T>(
        string targetName,
        string key,
        string instanceKey,
        T? value,
        TimeSpan expiration
    );

    bool Remove(string targetName, string key, string instanceKey);

    bool IsExpired(string targetName, string key, string instanceKey);

    void RemoveTarget(string targetName);
}
