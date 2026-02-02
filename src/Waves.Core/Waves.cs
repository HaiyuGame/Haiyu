using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Waves.Core.Contracts;
using Waves.Core.GameContext;
using Waves.Core.GameContext.Contexts;
using Waves.Core.GameContext.Contexts.PRG;
using Waves.Core.Services;

namespace Waves.Core;

public static class Waves
{
    /// <summary>
    /// 注入游戏上下文，注意已包含HttpClientFactory
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddGameContext(this IServiceCollection services)
    {
        services
            .AddTransient<IHttpClientService, HttpClientService>()
            .AddKeyedSingleton<IGameContext, WavesMainGameContext>(
                nameof(WavesMainGameContext),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetMainGameContext();
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    return context;
                }
            )
            .AddKeyedSingleton<IGameContext, WavesGlobalGameContext>(
                nameof(WavesGlobalGameContext),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetGlobalGameContext();
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    return context;
                }
            )
            .AddKeyedSingleton<IGameContext, WavesBiliBiliGameContext>(
                nameof(WavesBiliBiliGameContext),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetBilibiliGameContext();
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    return context;
                }
            )
            .AddKeyedSingleton<IGameContext, PunishMainGameContext>(
                nameof(PunishMainGameContext),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetMainPGRGameContext();
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    return context;
                }
            )
            .AddKeyedSingleton<IGameContext, PunishBiliBiliGameContext>(
                nameof(PunishBiliBiliGameContext),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetBiliBiliPRGGameContext();
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    return context;
                }
            )
            .AddKeyedSingleton<IGameContext, PunishGlobalGameContext>(
                nameof(PunishGlobalGameContext),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetGlobalPGRGameContext();
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    return context;
                }
            )
            .AddKeyedSingleton<IGameContext, PunishTwGameContext>(
                nameof(PunishTwGameContext),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetTwWavesGameContext();
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    return context;
                }
            )
            
            .AddTransient<IHttpClientService, HttpClientService>();
        return services;
    }

    public static async Task<IServiceCollection> AddGameContextAsync(this IServiceCollection services)
    {
        services.AddGameContext();

        using var provider = services.BuildServiceProvider();
        var contexts = new IGameContext[]
        {
            provider.GetRequiredKeyedService<IGameContext>(nameof(WavesMainGameContext)),
            provider.GetRequiredKeyedService<IGameContext>(nameof(WavesGlobalGameContext)),
            provider.GetRequiredKeyedService<IGameContext>(nameof(WavesBiliBiliGameContext)),
            provider.GetRequiredKeyedService<IGameContext>(nameof(PunishMainGameContext)),
            provider.GetRequiredKeyedService<IGameContext>(nameof(PunishBiliBiliGameContext)),
            provider.GetRequiredKeyedService<IGameContext>(nameof(PunishGlobalGameContext)),
            provider.GetRequiredKeyedService<IGameContext>(nameof(PunishTwGameContext))
        };

        foreach (var context in contexts)
        {
            await context.InitAsync().ConfigureAwait(false);
        }

        return services;
    }

}
