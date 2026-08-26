

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
        // AddGameContext 是完整的核心注册入口。TryAdd 保留宿主提供的自定义实现，
        // 同时保证控制台、测试项目只调用 AddGameContext 也能解析游戏上下文。
        services.TryAddSingleton<AppSettings>();
        services.TryAddSingleton<IIoCircuitBreaker, IoCircuitBreaker>();

        #region 新核心测试
        //事件订阅发布器
        services
            .AddSingleton<SystemEventPublisher>()
            .AddKeyedSingleton<GameEventPublisher>(nameof(PunishMainGameContextV2))
            .AddKeyedSingleton<IGameContextV2, PunishMainGameContextV2>(
                nameof(PunishMainGameContextV2),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetMainPunishGameContextV2(
                        provider.GetRequiredService<IIoCircuitBreaker>()
                    );
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    context.GameEventPublisher =
                        provider.GetRequiredKeyedService<GameEventPublisher>(
                            nameof(PunishMainGameContextV2)
                        );
                    context.SystemEventPublisher = provider.GetRequiredService<SystemEventPublisher>();
                    return context;
                }
            )
            .AddKeyedSingleton<GameEventPublisher>(nameof(PunishBiliBiliGameContextV2))
            .AddKeyedSingleton<IGameContextV2, PunishBiliBiliGameContextV2>(
                nameof(PunishBiliBiliGameContextV2),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetBiliBiliPunishGameContextV2(
                        provider.GetRequiredService<IIoCircuitBreaker>()
                    );
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    context.GameEventPublisher =
                        provider.GetRequiredKeyedService<GameEventPublisher>(
                            nameof(PunishBiliBiliGameContextV2)
                        );
                    context.SystemEventPublisher = provider.GetRequiredService<SystemEventPublisher>();
                    return context;
                }
            )
            .AddKeyedSingleton<GameEventPublisher>(nameof(PunishGlobalGameContextV2))
            .AddKeyedSingleton<IGameContextV2, PunishGlobalGameContextV2>(
                nameof(PunishGlobalGameContextV2),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetGlobalPunishGameContextV2(
                        provider.GetRequiredService<IIoCircuitBreaker>()
                    );
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    context.GameEventPublisher =
                        provider.GetRequiredKeyedService<GameEventPublisher>(
                            nameof(PunishGlobalGameContextV2)
                        );
                    context.SystemEventPublisher = provider.GetRequiredService<SystemEventPublisher>();
                    return context;
                }
            )
            .AddKeyedSingleton<GameEventPublisher>(nameof(PunishTwGameContextV2))
            .AddKeyedSingleton<IGameContextV2, PunishTwGameContextV2>(
                nameof(PunishTwGameContextV2),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetTwPunishGameContextV2(
                        provider.GetRequiredService<IIoCircuitBreaker>()
                    );
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    context.GameEventPublisher =
                        provider.GetRequiredKeyedService<GameEventPublisher>(
                            nameof(PunishTwGameContextV2)
                        );
                    context.SystemEventPublisher = provider.GetRequiredService<SystemEventPublisher>();
                    return context;
                }
            )
            .AddKeyedSingleton<GameEventPublisher>(nameof(WavesMainGameContextV2))
            .AddKeyedSingleton<IGameContextV2, WavesMainGameContextV2>(
                nameof(WavesMainGameContextV2),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetMainWavesGameContextV2(
                        provider.GetRequiredService<IIoCircuitBreaker>()
                    );
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    context.GameEventPublisher =
                        provider.GetRequiredKeyedService<GameEventPublisher>(
                            nameof(WavesMainGameContextV2)
                        );
                    context.SystemEventPublisher = provider.GetRequiredService<SystemEventPublisher>();
                    return context;
                }
            )
            .AddKeyedSingleton<GameEventPublisher>(nameof(WavesBiliBiliGameContextV2))
            .AddKeyedSingleton<IGameContextV2, WavesBiliBiliGameContextV2>(
                nameof(WavesBiliBiliGameContextV2),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetBilibiliWavesGameContextV2(
                        provider.GetRequiredService<IIoCircuitBreaker>()
                    );
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    context.GameEventPublisher =
                        provider.GetRequiredKeyedService<GameEventPublisher>(
                            nameof(WavesBiliBiliGameContextV2)
                        );
                    context.SystemEventPublisher = provider.GetRequiredService<SystemEventPublisher>();
                    return context;
                }
            )
            .AddKeyedSingleton<GameEventPublisher>(nameof(WavesGlobalGameContextV2))
            .AddKeyedSingleton<IGameContextV2, WavesGlobalGameContextV2>(
                nameof(WavesGlobalGameContextV2),
                (provider, c) =>
                {
                    var context = GameContextFactory.GetWavesGlobalGameContextV2(
                        provider.GetRequiredService<IIoCircuitBreaker>()
                    );
                    context.HttpClientService = provider.GetRequiredService<IHttpClientService>();
                    context.GameEventPublisher =
                        provider.GetRequiredKeyedService<GameEventPublisher>(
                            nameof(WavesGlobalGameContextV2)
                        );
                    context.SystemEventPublisher = provider.GetRequiredService<SystemEventPublisher>();
                    return context;
                }
            )
            .AddKeyedSingleton<CloudGameEventPublisher>(nameof(KuroCloudGameContext))
            .AddKeyedSingleton<IKuroCloudGameContext, KuroCloudGameContext>(
                nameof(KuroCloudGameContext),
                (provider, c) =>
                {
                    var context = new KuroCloudGameContext(provider.GetRequiredService<IWavesCloudGameService>());
                    context.GamerConfigPath = GameContextFactory.GameBassPath + "\\WavesCloudConfig";
                    context.CloudGameEventPublisher =
                        provider.GetRequiredKeyedService<CloudGameEventPublisher>(
                            nameof(KuroCloudGameContext)
                        );
                    return context;
                }
            )
        #endregion
            .AddTransient<IHttpClientService, HttpClientService>();
        return services;
    }
}
