using GrayExtension;

namespace Waves.Core.GameContext.ContextsV2.Waves;

public class WavesBiliBiliGameContextV2 : KuroGameContextBaseV2
{
    public override string GameContextNameKey => nameof(WavesBiliBiliGameContextV2);
    internal WavesBiliBiliGameContextV2(KuroGameApiConfig config, string name, IIoCircuitBreaker ioCircuitBreaker, GrayOpenService grayOpenService)
        : base(config, name, "鸣潮B服", ioCircuitBreaker,grayOpenService) { }

    public override Type ContextType => typeof(WavesBiliBiliGameContextV2);
    public override GameType GameType => Models.Enums.GameType.Waves;


    public override bool IsBunle { get; internal set; } = true;
}
