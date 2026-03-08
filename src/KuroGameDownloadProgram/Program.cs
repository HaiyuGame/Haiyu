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
using Waves.Core.Common;
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
var mainGame = host.Services.GetRequiredKeyedService<IGameContext>(nameof(PunishMainGameContext));
var locals = await mainGame.GetLocalGameOAuthAsync();
var key =  KrKeyHelper.Xor(locals[0].OauthCode, 5);
string json = await File.ReadAllTextAsync("D:\\Test.txt");
var result = await mainGame.QueryPlayerInfoAsync(key);
var result2 = await mainGame.QueryRoleInfoAsync(key, result.Items[0].Id, result.Items[0].ServerName);
Console.ReadLine();
