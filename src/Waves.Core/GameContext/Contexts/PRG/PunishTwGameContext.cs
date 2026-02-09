using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext.Contexts.PRG;

public sealed class PunishTwGameContext : KuroGameContextBase
{

    public override string GameContextNameKey => nameof(PunishTwGameContext);
    internal PunishTwGameContext(KuroGameApiConfig config)
        : base(config, nameof(PunishTwGameContext)) { }


    public override Type ContextType => typeof(PunishTwGameContext);
    public override GameType GameType => GameType.Punish;
}
