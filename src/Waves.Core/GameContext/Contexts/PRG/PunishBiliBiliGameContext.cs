using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext.Contexts;

public sealed class PunishBiliBiliGameContext: KuroGameContextBase
{
    public override string GameContextNameKey => nameof(PunishBiliBiliGameContext);
    public PunishBiliBiliGameContext(KuroGameApiConfig config)
    : base(config, nameof(PunishBiliBiliGameContext)) { }

    public override Type ContextType => typeof(PunishBiliBiliGameContext);
    public override GameType GameType => GameType.Punish;
}
