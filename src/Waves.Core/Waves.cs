using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Waves.Core.Contracts;
using Waves.Core.GameContext;
using Waves.Core.GameContext.ContextsV2;
using Waves.Core.GameContext.ContextsV2.Punish;
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
        #region 新核心测试
        //事件订阅发布器
        services
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
            .AddKeyedSingleton<GameEventPublisher>(nameof(PunishBiliBiliGameContextV2))
            .AddKeyedSingleton<IGameContextV2, PunishBiliBiliGameContextV2>(
                nameof(PunishBiliBiliGameContextV2),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetBiliBiliPunishGameContextV2();
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    context.GameEventPublisher =
                        provider.GetRequiredKeyedService<GameEventPublisher>(
                            nameof(PunishBiliBiliGameContextV2)
                        );
                    return context;
                }
            )
            .AddKeyedSingleton<GameEventPublisher>(nameof(PunishGlobalGameContextV2))
            .AddKeyedSingleton<IGameContextV2, PunishGlobalGameContextV2>(
                nameof(PunishGlobalGameContextV2),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetGlobalPunishGameContextV2();
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    context.GameEventPublisher =
                        provider.GetRequiredKeyedService<GameEventPublisher>(
                            nameof(PunishGlobalGameContextV2)
                        );
                    return context;
                }
            )
            .AddKeyedSingleton<GameEventPublisher>(nameof(PunishTwGameContextV2))
            .AddKeyedSingleton<IGameContextV2, PunishTwGameContextV2>(
                nameof(PunishTwGameContextV2),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetTwPunishGameContextV2();
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    context.GameEventPublisher =
                        provider.GetRequiredKeyedService<GameEventPublisher>(
                            nameof(PunishTwGameContextV2)
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
