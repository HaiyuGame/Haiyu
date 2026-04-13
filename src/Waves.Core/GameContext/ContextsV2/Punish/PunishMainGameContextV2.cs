using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext.ContextsV2.Punish;

public class PunishMainGameContextV2 : KuroGameContextBaseV2
{
    public override string GameContextNameKey => nameof(PunishMainGameContextV2);
    internal PunishMainGameContextV2(KuroGameApiConfig config,string name)
        : base(config, name) { }

    public override Type ContextType => typeof(PunishMainGameContextV2);
    public override GameType GameType => GameType.Punish;

}
