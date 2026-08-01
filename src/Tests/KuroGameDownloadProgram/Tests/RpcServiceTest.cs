using Haiyu.ServiceHost;
using Haiyu.ServiceHost.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Waves.Core;
using Waves.Core.Contracts;
using Waves.Core.Services;
using Waves.Settings;
using WavesLauncher.Core.Contracts;

namespace KuroGameDownloadProgram.Tests;

public static class RpcServiceTest
{
    public static IHost DefaultHost { get; private set; }

    public static async Task BuildService()
    {
        DefaultHost = Microsoft
            .Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices(
                (s) =>
                {
                    s.AddSingleton<AppSettings>();
                    s.AddSingleton<RpcSettings>();
                    s.AddHostedService<RpcService>(
                        (s) =>
                        {
                            RpcService service = new RpcService(
                                s.GetRequiredKeyedService<LoggerService>("AppLog"),
                                s.GetRequiredService<RpcSettings>()
                            );
                            service.RegisterMethod(
                                s.GetRequiredService<IRpcMethodService>().Method
                            );
                            return service;
                        }
                    );
                    s.AddKeyedSingleton<LoggerService>(
                        "AppLog",
                        (s, e) =>
                        {
                            var logger = new LoggerService();
                            logger.InitLogger(AppSettings.LogPath, Serilog.RollingInterval.Day);
                            return logger;
                        }
                    );
                    s.AddSingleton<IKuroClient, KuroClient>();
                    s.AddSingleton<IKuroAccountService, KuroAccountService>();
                    s.AddSingleton<ICloudGameService, CloudGameService>();
                    s.AddGameContext();
                }
            )
            .Build();
        await DefaultHost.RunAsync();
    }
}
