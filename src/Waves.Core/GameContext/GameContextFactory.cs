using Waves.Core.Contracts;
using Waves.Core.GameContext.Contexts;
using Waves.Core.GameContext.Contexts.PRG;
using Waves.Core.GameContext.ContextsV2;
using Waves.Core.Models.CoreApi;
using Waves.Core.Services;

namespace Waves.Core.GameContext;

public static class GameContextFactory
{
    public static string GameBassPath { get; set; }

    /// <summary>
    /// 获得全部支持的游戏上下文名称
    /// </summary>
    /// <returns></returns>
    public static IReadOnlyCollection<String> GameAllContext() =>
        [
            nameof(WavesMainGameContext),
            nameof(WavesBiliBiliGameContext),
            nameof(WavesGlobalGameContext),
            nameof(PunishMainGameContext),
            nameof(PunishBiliBiliGameContext),
            nameof(PunishGlobalGameContext),
            nameof(PunishTwGameContext),
        ];

    internal static WavesBiliBiliGameContext GetBilibiliGameContext() =>
        new WavesBiliBiliGameContext(KuroGameApiConfig.BilibiliConfig)
        {
            GamerConfigPath = GameContextFactory.GameBassPath + "\\BiliBiliConfig",
            IsLimitSpeed = false,
        };

    internal static WavesGlobalGameContext GetGlobalGameContext() =>
        new WavesGlobalGameContext(KuroGameApiConfig.GlobalConfig)
        {
            GamerConfigPath = GameContextFactory.GameBassPath + "\\GlobalConfig",
            IsLimitSpeed = false,
        };

    internal static WavesMainGameContext GetMainGameContext() =>
        new WavesMainGameContext(KuroGameApiConfig.MainAPiConfig)
        {
            GamerConfigPath = GameContextFactory.GameBassPath + "\\MainConfig",
            IsLimitSpeed = false,
        };

    internal static PunishMainGameContext GetMainPGRGameContext() =>
        new PunishMainGameContext(KuroGameApiConfig.MainBGRConfig)
        {
            GamerConfigPath = GameContextFactory.GameBassPath + "\\MainPGRConfig",
            IsLimitSpeed = false,
        };

    internal static PunishBiliBiliGameContext GetBiliBiliPRGGameContext() =>
        new PunishBiliBiliGameContext(KuroGameApiConfig.BiliBiliBGRConfig)
        {
            GamerConfigPath = GameContextFactory.GameBassPath + "\\BilibiliPRGConfig",
            IsLimitSpeed = false,
        };

    internal static PunishGlobalGameContext GetGlobalPGRGameContext() =>
        new PunishGlobalGameContext(KuroGameApiConfig.GlobalBGRConfig)
        {
            GamerConfigPath = GameContextFactory.GameBassPath + "\\GlokbalPGRConfig",
            IsLimitSpeed = false,
        };

    internal static PunishTwGameContext GetTwWavesGameContext() =>
        new PunishTwGameContext(KuroGameApiConfig.TWBGRConfig)
        {
            GamerConfigPath = GameContextFactory.GameBassPath + "\\TwPGRConfig",
            IsLimitSpeed = false,
        };

    #region 新核心

    internal static ContextsV2.PunishMainGameContextV2 GetMainPunishGameContextV2() =>
        new ContextsV2.PunishMainGameContextV2(KuroGameApiConfig.MainBGRConfig, nameof(ContextsV2.PunishMainGameContextV2))
        {
            GamerConfigPath = GameContextFactory.GameBassPath + "\\MainPGRV2Config",
            IsLimitSpeed = false,
        };

    internal static ContextsV2.WavesMainGameContextV2 GetMainWavesGameContextV2() =>
       new ContextsV2.WavesMainGameContextV2(KuroGameApiConfig.MainAPiConfig, nameof(ContextsV2.WavesMainGameContextV2))
       {
           GamerConfigPath = GameContextFactory.GameBassPath + "\\MainWavesV2Config",
           IsLimitSpeed = false,
       };
    #endregion
}
