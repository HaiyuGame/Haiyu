using CommunityToolkit.Mvvm.ComponentModel;
using Haiyu.Plugin.Services;
using Haiyu.RpcClient;
using KuroGameDownloadProgram;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using Waves.Api.Models;
using Waves.Api.Models.GameWikiiClient;
using Waves.Core;
using Waves.Core.Contracts;
using Waves.Core.GameContext;
using Waves.Core.GameContext.Contexts;
using Waves.Core.Helpers;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Services;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddGameContext();
    })
    .Build();
GameContextFactory.GameBassPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Waves";
var mainGame = host.Services.GetRequiredKeyedService<IGameContext>(nameof(WavesMainGameContext));
mainGame.GameContextProdOutput += ProdDownload;
mainGame.GameContextOutput+= GameContextOutput;

async Task GameContextOutput(object sender, GameContextOutputArgs args)
{
    Console.WriteLine($"{args.Type},{args.ProgressPercentage}，VerifySpeed:{args.VerifySpeed},DownloadSpeed:{args.DownloadSpeed},剩余{args.RemainingTime}");
}

async Task ProdDownload(object sender, GameContextOutputArgs args)
{
    Console.WriteLine($"{args.Type},{args.ProgressPercentage}，VerifySpeed:{args.VerifySpeed},DownloadSpeed:{args.DownloadSpeed},剩余{args.RemainingTime}");
}

await mainGame.InitAsync();
var launcher = await mainGame.GetGameLauncherSourceAsync();
await mainGame.StartDownloadProdGame(launcher,"D:\\Predown");
Console.ReadLine();
