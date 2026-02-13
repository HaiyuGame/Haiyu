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
using Waves.Core.Settings;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddGameContext();
        services.AddSingleton<CloudGameService>();
        services.AddSingleton<CloudConfigManager>((s) =>
        {
            return new CloudConfigManager(AppSettings.CloudFolderPath);
        });
        services.AddSingleton<IHttpClientService,HttpClientService>();
        services.AddSingleton<AppSettings>();
    })
    .Build();
GameContextFactory.GameBassPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Waves";
var mainGame = host.Services.GetRequiredService<CloudGameService>();
var result =  await mainGame.ConfigManager.GetUsersAsync();
await mainGame.OpenUserAsync(result[0]);
await mainGame.GetUserInfoAsync(result[0]);
Console.ReadLine();
