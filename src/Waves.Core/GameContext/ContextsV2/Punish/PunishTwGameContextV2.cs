using GrayExtension;

namespace Waves.Core.GameContext.ContextsV2.Punish;

public class PunishTwGameContextV2 : KuroGameContextBaseV2
{
    public override string GameContextNameKey => nameof(PunishTwGameContextV2);

    public PunishTwGameContextV2(
        KuroGameApiConfig config,
        string name,
        IIoCircuitBreaker ioCircuitBreaker,
        GrayOpenService grayOpenService
    )
        : base(config, name, "战双台服", ioCircuitBreaker, grayOpenService) { }

    public override Type ContextType => typeof(PunishTwGameContextV2);
    public override GameType GameType => GameType.Punish;
    public override bool IsBunle { get; internal set; } = false;
}
