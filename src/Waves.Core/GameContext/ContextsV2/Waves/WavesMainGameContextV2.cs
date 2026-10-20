using GrayExtension;

namespace Waves.Core.GameContext.ContextsV2.Waves;

public class WavesMainGameContextV2: KuroGameContextBaseV2
{
    public override string GameContextNameKey => nameof(WavesMainGameContextV2);
    internal WavesMainGameContextV2(KuroGameApiConfig config, string name, IIoCircuitBreaker ioCircuitBreaker, GrayOpenService grayOpenService)
        : base(config, name, "鸣潮官服", ioCircuitBreaker,grayOpenService) { }

    public override Type ContextType => typeof(WavesMainGameContextV2);
    public override GameType GameType => GameType.Waves;


    public override bool IsBunle { get; internal set; } = true;
}
