namespace Waves.Core.GameContext.ContextsV2.Punish;

public class PunishMainGameContextV2 : KuroGameContextBaseV2
{
    public override string GameContextNameKey => nameof(PunishMainGameContextV2);
    internal PunishMainGameContextV2(KuroGameApiConfig config,string name,IIoCircuitBreaker ioCircuitBreaker)
        : base(config, name, "战双官服", ioCircuitBreaker) { }

    public override Type ContextType => typeof(PunishMainGameContextV2);
    public override GameType GameType => GameType.Punish;

}
