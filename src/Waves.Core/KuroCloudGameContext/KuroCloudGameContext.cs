using Waves.Core.Contracts;
using Waves.Core.Contracts.CloudGame;

namespace Waves.Core;

public class KuroCloudGameContext : IKuroCloudGameContext
{
    public IWavesCloudGameService CloudGameService { get; }

    public KuroCloudGameContext(IWavesCloudGameService cloudGameService)
    {
        CloudGameService = cloudGameService;
        
    }

    
}
