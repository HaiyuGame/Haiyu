namespace Waves.Core.GameContext.ContextsV2.Punish;

public class PunishBiliBiliGameContextV2: KuroGameContextBaseV2
{
    public override string GameContextNameKey => nameof(PunishBiliBiliGameContextV2);
    public PunishBiliBiliGameContextV2(KuroGameApiConfig config, string name, IIoCircuitBreaker ioCircuitBreaker)
        : base(config, name, "战双BiliBili", ioCircuitBreaker) { }

    public override Type ContextType => typeof(PunishBiliBiliGameContextV2);
    public override GameType GameType => GameType.Punish;
}
