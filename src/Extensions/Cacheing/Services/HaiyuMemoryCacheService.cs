using System.Collections.Concurrent;
using Cacheing.Contracts;
using Microsoft.Extensions.Caching.Memory;

namespace Cacheing.Services;

public sealed class HaiyuMemoryCacheService : IHaiyuMemoryCacheService
{
    private static readonly object NullValue = new();
    private readonly ConcurrentDictionary<string, long> _targetVersions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<CacheKey, Lazy<Task<object>>> _inflight = new();

    public HaiyuMemoryCacheService(IMemoryCache memoryCache)
    {
        MemoryCache = memoryCache;
    }

    private IMemoryCache MemoryCache { get; }

    public async Task<T?> GetOrCreateAsync<T>(
        string targetName,
        string key,
        string instanceKey,
        TimeSpan expiration,
        Func<CancellationToken, Task<T?>> factory,
        HaiyuCacheMode mode = HaiyuCacheMode.Default,
        CancellationToken cancellationToken = default
    )
    {
        ValidateArguments(targetName, key, instanceKey, expiration);
        ArgumentNullException.ThrowIfNull(factory);

        var cacheKey = CreateKey(targetName, key, instanceKey);
        if (mode != HaiyuCacheMode.Refresh
            && MemoryCache.TryGetValue(cacheKey, out object? cached))
        {
            return Unwrap<T>(cached);
        }

        if (mode == HaiyuCacheMode.CacheOnly)
        {
            return default;
        }

        var candidate = new Lazy<Task<object>>(
            () => LoadAsync(cacheKey, expiration, factory, cancellationToken),
            LazyThreadSafetyMode.ExecutionAndPublication
        );
        var lazy = _inflight.GetOrAdd(
            cacheKey,
            candidate
        );

        if (ReferenceEquals(lazy, candidate))
        {
            _ = lazy.Value.ContinueWith(
                (_, state) =>
                {
                    var cleanup = (InflightCleanup)state!;
                    cleanup.Owner._inflight.TryRemove(
                        new KeyValuePair<CacheKey, Lazy<Task<object>>>(cleanup.Key, cleanup.Value)
                    );
                },
                new InflightCleanup(this, cacheKey, lazy),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
        }

        var value = await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        return Unwrap<T>(value);
    }

    public void Set<T>(
        string targetName,
        string key,
        string instanceKey,
        T? value,
        TimeSpan expiration
    )
    {
        ValidateArguments(targetName, key, instanceKey, expiration);
        MemoryCache.Set(CreateKey(targetName, key, instanceKey), Wrap(value), expiration);
    }

    public bool Remove(string targetName, string key, string instanceKey)
    {
        ValidateNames(targetName, key, instanceKey);
        var cacheKey = CreateKey(targetName, key, instanceKey);
        var existed = MemoryCache.TryGetValue(cacheKey, out _);
        MemoryCache.Remove(cacheKey);
        return existed;
    }

    public bool IsExpired(string targetName, string key, string instanceKey)
    {
        ValidateNames(targetName, key, instanceKey);
        return !MemoryCache.TryGetValue(CreateKey(targetName, key, instanceKey), out _);
    }

    public void RemoveTarget(string targetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        _targetVersions.AddOrUpdate(targetName, 1, static (_, version) => checked(version + 1));
    }

    private async Task<object> LoadAsync<T>(
        CacheKey cacheKey,
        TimeSpan expiration,
        Func<CancellationToken, Task<T?>> factory,
        CancellationToken cancellationToken
    )
    {
        var value = await factory(cancellationToken).ConfigureAwait(false);
        var wrapped = Wrap(value);
        MemoryCache.Set(cacheKey, wrapped, expiration);
        return wrapped;
    }

    private CacheKey CreateKey(string targetName, string key, string instanceKey)
    {
        var version = _targetVersions.GetOrAdd(targetName, 0);
        return new CacheKey(targetName, key, instanceKey, version);
    }

    private static object Wrap<T>(T? value) => value is null ? NullValue : value;

    private static T? Unwrap<T>(object? value)
    {
        if (value is null || ReferenceEquals(value, NullValue))
        {
            return default;
        }

        return (T)value;
    }

    private static void ValidateArguments(
        string targetName,
        string key,
        string instanceKey,
        TimeSpan expiration
    )
    {
        ValidateNames(targetName, key, instanceKey);
        if (expiration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiration), "Expiration must be positive.");
        }
    }

    private static void ValidateNames(string targetName, string key, string instanceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(instanceKey);
    }

    private readonly record struct CacheKey(
        string TargetName,
        string Key,
        string InstanceKey,
        long TargetVersion
    );

    private sealed record InflightCleanup(
        HaiyuMemoryCacheService Owner,
        CacheKey Key,
        Lazy<Task<object>> Value
    );
}
