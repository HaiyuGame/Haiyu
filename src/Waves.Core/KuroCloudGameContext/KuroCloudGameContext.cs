using Waves.Core.Contracts;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.Services.CloudGameServices;

namespace Waves.Core;

public class KuroCloudGameContext : IKuroCloudGameContext
{

    public KuroCloudGameContext(WavesCloudSurvivalService cloudGameService)
    {
        WavesCloudSurivivalService = cloudGameService;
    }


    public WavesCloudSurvivalService WavesCloudSurivivalService { get; }

    public async Task InitAsync()
    {
        if (WavesCloudSurivivalService.IsRuning)
        {
            await WavesCloudSurivivalService.StopAsync();
        }
        else
        {
            await WavesCloudSurivivalService.StartAsync();
        }
    }
}
