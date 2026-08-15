using Cacheing.Contracts;
using Cacheing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cacheing;

public static class CacheingHostBuilderExtensions
{
    public static IHostBuilder RegisterCache(this IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ConfigureServices((_, services) =>
        {
            services.AddMemoryCache();
            services.AddSingleton<IHaiyuMemoryCacheService, HaiyuMemoryCacheService>();
        });
    }
}
