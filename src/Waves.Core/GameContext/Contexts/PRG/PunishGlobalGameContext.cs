using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext.Contexts;

public sealed class PunishGlobalGameContext:KuroGameContextBase
{


    public override string GameContextNameKey => nameof(PunishGlobalGameContext);
    public PunishGlobalGameContext(KuroGameApiConfig config)
    : base(config, nameof(PunishGlobalGameContext)) { }

    public override Type ContextType => typeof(PunishGlobalGameContext);
    public override GameType GameType => GameType.Punish;
}
