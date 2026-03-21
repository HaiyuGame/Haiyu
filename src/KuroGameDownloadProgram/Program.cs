using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Haiyu.Plugin.Services;
using Haiyu.RpcClient;
using KuroGameDownloadProgram;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Waves.Api.Models;
using Waves.Api.Models.GameWikiiClient;
using Waves.Core;
using Waves.Core.Common;
using Waves.Core.Contracts;
using Waves.Core.GameContext;
using Waves.Core.GameContext.Contexts;
using Waves.Core.Helpers;
using Waves.Core.Models;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Downloader;
using Waves.Core.Services;
using Waves.Core.Settings;

GameContextFactory.GameBassPath =
    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Waves";

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddGameContext();
        services.AddSingleton<CloudGameService>();
        services.AddSingleton<CloudConfigManager>(
            (s) =>
            {
                return new CloudConfigManager(AppSettings.CloudFolderPath);
            }
        );
        services.AddSingleton<IHttpClientService, HttpClientService>();
        services.AddSingleton<AppSettings>();
        services.AddKeyedSingleton<LoggerService>(
            "AppLog",
            (s, e) =>
            {
                var logger = new LoggerService();
                logger.InitLogger(AppSettings.LogPath, Serilog.RollingInterval.Day);
                return logger;
            }
        );
    })
    .Build();

//var mainGame = host.Services.GetRequiredKeyedService<IGameContext>(nameof(PunishMainGameContext));
//await mainGame.InitAsync();
//var launcher = await mainGame.GetGameLauncherSourceAsync(null);
//var previous = launcher
//            .ResourceDefault.Config.PatchConfig.Last();

var v2 = host.Services.GetService<V2TestGameContext>();
await v2.InitAsync();
await v2.StartDownloadTaskAsync("D:\\Punish");
await Task.Delay(5000);
Console.WriteLine("正在下载，按回车键取消下载，输入Q停止");
if(Console.ReadLine() == "Q")
    await v2.StopCannelTaskAsync();
Console.ReadLine();

public class TestContext : KuroGameContextBaseV2
{
    public TestContext(KuroGameApiConfig config, string contextName)
        : base(config, contextName) { }
}
