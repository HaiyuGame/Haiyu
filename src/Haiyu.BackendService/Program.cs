using Haiyu.BackendService;
using Haiyu.ServiceHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Waves.Core;

var builder = Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureServices(services =>
    {
        services.AddGameContext();
        services.AddSingleton<RpcService>();
        services.AddSingleton<BackendEventSink>();
        services.AddSingleton<BackendGameContextService>();
        services.AddSingleton<BackendRpcMethods>();
        services.AddHostedService<BackendLifetimeService>();
        services.AddHostedService(sp => sp.GetRequiredService<RpcService>());
    });

var host = builder.Build();

var rpcService = host.Services.GetRequiredService<RpcService>();
var methodRegistry = host.Services.GetRequiredService<BackendRpcMethods>();

rpcService.RegisterMethod(methodRegistry.CreateMethods());

await host.RunAsync();
