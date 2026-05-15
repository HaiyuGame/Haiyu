using Waves.Core.Contracts;
using Waves.Core.Contracts.CloudGame;

namespace Waves.Core.KuroCloudGameContext;

public class KuroCloudGameContext : IKuroCloudGameContext
{
    public ICloudGameService CloudGameService { get; }

    public KuroCloudGameContext(ICloudGameService cloudGameService)
    {
        CloudGameService = cloudGameService;
        
    }
}
