using Waves.Core.Contracts;
using Waves.Core.GameContext.Contexts;
using Waves.Core.GameContext.Contexts.PRG;
using Waves.Core.GameContext.ContextsV2.Waves;
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
            nameof(PunishMainGameContext),
            nameof(PunishBiliBiliGameContext),
            nameof(PunishGlobalGameContext),
            nameof(PunishTwGameContext),
        ];


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

    internal static ContextsV2.Waves.WavesMainGameContextV2 GetMainWavesGameContextV2() =>
       new ContextsV2.Waves.WavesMainGameContextV2(KuroGameApiConfig.MainAPiConfig, nameof(WavesMainGameContextV2))
       {
           GamerConfigPath = GameContextFactory.GameBassPath + "\\MainConfig",
           IsLimitSpeed = false,
       }; 
    internal static ContextsV2.Waves.WavesBiliBiliGameContextV2 GetBilibiliWavesGameContextV2() =>
       new ContextsV2.Waves.WavesBiliBiliGameContextV2(KuroGameApiConfig.BilibiliConfig, nameof(WavesBiliBiliGameContextV2))
       {
           GamerConfigPath = GameContextFactory.GameBassPath + "\\BilibiliWavesV2Config",
           IsLimitSpeed = false,
       }; 
    internal static ContextsV2.Waves.WavesGlobalGameContextV2 GetWavesGlobalGameContextV2() =>
       new ContextsV2.Waves.WavesGlobalGameContextV2(KuroGameApiConfig.GlobalConfig, nameof(WavesGlobalGameContextV2))
       {
           GamerConfigPath = GameContextFactory.GameBassPath + "\\BilibiliWavesV2Config",
           IsLimitSpeed = false,
       };
    #endregion
}
