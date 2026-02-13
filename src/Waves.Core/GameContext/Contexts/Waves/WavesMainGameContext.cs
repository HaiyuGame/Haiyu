using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext.Contexts;

public sealed class WavesMainGameContext : KuroGameContextBase
{
    public override string GameContextNameKey => nameof(WavesMainGameContext);
    internal WavesMainGameContext(KuroGameApiConfig config)
        : base(config, nameof(WavesMainGameContext)) { }

    public override Type ContextType => typeof(WavesMainGameContext);
    public override GameType GameType => GameType.Waves;
}
