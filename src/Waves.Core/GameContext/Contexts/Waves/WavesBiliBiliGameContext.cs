using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext.Contexts;

public sealed class WavesBiliBiliGameContext : KuroGameContextBase
{
    public override string GameContextNameKey => nameof(WavesBiliBiliGameContext);
    internal WavesBiliBiliGameContext(KuroGameApiConfig config)
        : base(config, nameof(WavesBiliBiliGameContext)) { }

    public override Type ContextType => typeof(WavesBiliBiliGameContext);

    public override GameType GameType =>  GameType.Waves;
}
