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

var v2 = host.Services.GetService<V2TestGameContext>();
await v2.InitAsync();
await v2.StartDownloadTaskAsync("D:\\Punish");
var subMessage = await v2.GameEventPublisher.SubscribeAsync(Message);
Console.WriteLine("正在下载，按回车键取消下载，输入Q停止");

async ValueTask Message(GameContextOutputArgs args)
{
    Console.WriteLine(
        $"类型：{args.Type}:下载速度:{Math.Round(args.DownloadSpeed / 1024 / 1024, 2)}MB,校验速度：{Math.Round(args.VerifySpeed)}，剩余：{Math.Round((double)(args.TotalSize - args.CurrentSize) / 1024 / 1024 / 1024, 2)}GB"
    );
}

if (Console.ReadLine() == "Q")
{
    await v2.StopCannelTaskAsync();
    subMessage.Dispose();
}
Console.ReadLine();

public class TestContext : KuroGameContextBaseV2
{
    public TestContext(KuroGameApiConfig config, string contextName)
        : base(config, contextName) { }
}
