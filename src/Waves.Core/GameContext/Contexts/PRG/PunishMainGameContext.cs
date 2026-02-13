using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext.Contexts;

/// <summary>
/// 战双国服
/// </summary>
public sealed class PunishMainGameContext : KuroGameContextBase
{
    public override string GameContextNameKey => nameof(PunishMainGameContext);
    public PunishMainGameContext(KuroGameApiConfig config)
        : base(config, nameof(PunishMainGameContext)) { }

    public override Type ContextType => typeof(PunishMainGameContext);
    public override GameType GameType => GameType.Punish;
}
