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
using Waves.Core.Models.Downloader;
using Waves.Core.Services;

//IHost host = Host.CreateDefaultBuilder(args)
//    .ConfigureServices(services =>
//    {
//        services.AddHostedService<WebSocketRpcClient>((s) =>
//        {
//            WebSocketRpcClient socket = new WebSocketRpcClient();
//            socket.InitAsync("9084", "123456").Wait();
//            return socket;
//        });
//    })
//    .Build();

//var socket = host.Services.GetService<WebSocketRpcClient>();
//await host.StartAsync();

var id =  HardwareIdGenerator.GenerateUniqueId();

Console.ReadLine();
