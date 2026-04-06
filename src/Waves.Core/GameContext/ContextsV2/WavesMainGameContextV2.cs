using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext.ContextsV2;

public class WavesMainGameContextV2: KuroGameContextBaseV2
{
    public override string GameContextNameKey => nameof(WavesMainGameContextV2);
    internal WavesMainGameContextV2(KuroGameApiConfig config, string name)
        : base(config, name) { }

    public override Type ContextType => typeof(WavesMainGameContextV2);
    public override GameType GameType => GameType.Punish;
}
