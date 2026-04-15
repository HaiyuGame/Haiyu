using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext.ContextsV2.Punish;

public class PunishBiliBiliGameContextV2: KuroGameContextBaseV2
{
    public override string GameContextNameKey => nameof(PunishBiliBiliGameContextV2);
    public PunishBiliBiliGameContextV2(KuroGameApiConfig config, string name)
        : base(config, name) { }

    public override Type ContextType => typeof(PunishBiliBiliGameContextV2);
    public override GameType GameType => GameType.Punish;
}
