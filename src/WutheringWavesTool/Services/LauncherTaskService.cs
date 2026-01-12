using Waves.Core.Services;

namespace Haiyu.Services;

public sealed class LauncherTaskService : ILauncherTaskService
{
    public LauncherTaskService(
        ITipShow tipShow,
        [FromKeyedServices("AppLog")] LoggerService loggerService,
        IKuroClient wavesClient,AppSettings appSettings
    )
    {
        TipShow = tipShow;
        LoggerService = loggerService;
        WavesClient = wavesClient;
        AppSettings = appSettings;
    }

    public ITipShow TipShow { get; }
    public LoggerService LoggerService { get; }
    public IKuroClient WavesClient { get; }
    public AppSettings AppSettings { get; }

    public async Task RunAsync(CancellationToken token)
    {
        try
        {
            if (Boolean.TryParse(AppSettings.AutoSignCommunity, out var flag) && flag)
            {
                if (!(await WavesClient.IsLoginAsync(token)))
                {
                    await TipShow.ShowMessageAsync(
                        "请登录库洛通行证以启动自动签到",
                        Symbol.Message
                    );
                    return;
                }
                int signErrorCount = 0;
                var gamers = await WavesClient.GetGamerAsync( Waves.Core.Models.Enums.GameType.Waves,token);
                var Punish = await WavesClient.GetGamerAsync( Waves.Core.Models.Enums.GameType.Punish,token);
                if (gamers == null || gamers.Success == false || Punish == null || Punish.Success == false)
                {
                    return;
                }
                foreach (var item in gamers.Data.Concat(Punish.Data))
                {
                    var result = await WavesClient.SignInAsync(
                        item,
                        token
                    );
                    if (result is null || (!result.Success && result.Code != 1511))
                    {
                        signErrorCount++;
                    }
                }
                await TipShow.ShowMessageAsync(
                    $"自动签到结果：{gamers.Data.Count - signErrorCount}个签到成功",
                    Symbol.Message
                );
            }
        }
        catch (Exception ex)
        {
            LoggerService.WriteError(ex.Message);
        }
    }
}
