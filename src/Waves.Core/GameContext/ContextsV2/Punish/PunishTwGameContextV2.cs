using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext.ContextsV2.Punish;

public class PunishTwGameContextV2 : KuroGameContextBaseV2
{
    public override string GameContextNameKey => nameof(PunishTwGameContextV2);
    public PunishTwGameContextV2(KuroGameApiConfig config, string name)
        : base(config, name) { }

    public override Type ContextType => typeof(PunishTwGameContextV2);
    public override GameType GameType => GameType.Punish;
}
