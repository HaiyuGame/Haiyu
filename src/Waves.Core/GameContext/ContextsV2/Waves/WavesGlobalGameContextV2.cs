namespace Waves.Core.GameContext.ContextsV2.Waves;

public class WavesGlobalGameContextV2 : KuroGameContextBaseV2
{
    public override string GameContextNameKey => nameof(WavesGlobalGameContextV2);
    internal WavesGlobalGameContextV2(KuroGameApiConfig config, string name, IIoCircuitBreaker ioCircuitBreaker)
        : base(config, name, "鸣潮国际服", ioCircuitBreaker) { }

    public override Type ContextType => typeof(WavesGlobalGameContextV2);
    public override GameType GameType => Models.Enums.GameType.Waves;
}
