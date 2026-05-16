using Waves.Core.Contracts;
using Waves.Core.Contracts.CloudGame;

namespace Waves.Core;

public class KuroCloudGameContext : IKuroCloudGameContext
{
    public ICloudGameService CloudGameService { get; }

    public KuroCloudGameContext(ICloudGameService cloudGameService)
    {
        CloudGameService = cloudGameService;
        
    }

    public async Task CheckLocalUserAsync(CancellationToken token = default)
    {
        var users = await CloudGameService.ConfigManager.GetUsersAsync(token);
        foreach (var user in users)
        {
            var flage = await CloudGameService.OpenUserAsync(user, token);
            if (!flage.Item1)
            {
                await CloudGameService.ConfigManager.DeleteUserAsync(user.Sdkuserid);
            }
        }
    }
}
