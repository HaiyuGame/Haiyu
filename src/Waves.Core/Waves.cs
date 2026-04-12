using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Waves.Core.Contracts;
using Waves.Core.GameContext;
using Waves.Core.GameContext.Contexts;
using Waves.Core.GameContext.Contexts.PRG;
using Waves.Core.GameContext.ContextsV2;
using Waves.Core.GameContext.ContextsV2.Waves;
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
            #region 新核心测试
            //事件订阅发布器
            .AddKeyedSingleton<GameEventPublisher>(nameof(PunishMainGameContextV2))
            .AddKeyedSingleton<IGameContextV2, PunishMainGameContextV2>(
                nameof(PunishMainGameContextV2),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetMainPunishGameContextV2();
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    context.GameEventPublisher =
                        provider.GetRequiredKeyedService<GameEventPublisher>(
                            nameof(PunishMainGameContextV2)
                        );
                    return context;
                }
            )
            .AddKeyedSingleton<GameEventPublisher>(nameof(WavesMainGameContextV2))
            .AddKeyedSingleton<IGameContextV2, WavesMainGameContextV2>(
                nameof(WavesMainGameContextV2),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetMainWavesGameContextV2();
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    context.GameEventPublisher =
                        provider.GetRequiredKeyedService<GameEventPublisher>(
                            nameof(WavesMainGameContextV2)
                        );
                    return context;
                }
            )
            .AddKeyedSingleton<GameEventPublisher>(nameof(WavesBiliBiliGameContextV2))
            .AddKeyedSingleton<IGameContextV2, WavesBiliBiliGameContextV2>(
                nameof(WavesBiliBiliGameContextV2),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetBilibiliWavesGameContextV2();
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    context.GameEventPublisher =
                        provider.GetRequiredKeyedService<GameEventPublisher>(
                            nameof(WavesBiliBiliGameContextV2)
                        );
                    return context;
                }
            )
            .AddKeyedSingleton<GameEventPublisher>(nameof(WavesGlobalGameContextV2))
            .AddKeyedSingleton<IGameContextV2, WavesGlobalGameContextV2>(
                nameof(WavesGlobalGameContextV2),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetWavesGlobalGameContextV2();
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    context.GameEventPublisher =
                        provider.GetRequiredKeyedService<GameEventPublisher>(
                            nameof(WavesGlobalGameContextV2)
                        );
                    return context;
                }
            )
            #endregion
            .AddTransient<IHttpClientService, HttpClientService>();
        return services;
    }
}
