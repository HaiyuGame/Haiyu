using Microsoft.Extensions.DependencyInjection;
using Waves.Core.Contracts;
using Waves.Core.GameContext;
using Waves.Core.GameContext.Contexts;
using Waves.Core.GameContext.Contexts.PRG;
using Waves.Core.GameContext.ContextsV2;
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
            #region 新核心测试
            //事件订阅发布器
            .AddKeyedSingleton<GameEventPublisher>(nameof(PunishMainGameContextV2))
            .AddKeyedSingleton<GameEventPublisher>(nameof(WavesMainGameContextV2))
            .AddKeyedSingleton<IGameContextV2, PunishMainGameContextV2>(
                nameof(PunishMainGameContextV2),
                (provider,c) =>
                {
                    var context = GameContextFactory.GetMainPunishGameContextV2();
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    context.GameEventPublisher =
                        provider.GetRequiredKeyedService<GameEventPublisher>(
                            nameof(PunishMainGameContextV2)
                        );
                    return context;
                }
            ).AddKeyedSingleton<IGameContextV2, WavesMainGameContextV2>(
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
        #endregion
            .AddTransient<IHttpClientService, HttpClientService>();
        return services;
    }
}
