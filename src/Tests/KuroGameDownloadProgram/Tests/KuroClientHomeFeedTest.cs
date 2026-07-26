using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Waves.Api.Models.KuroClient.Options;
using Waves.Core;
using Waves.Core.Contracts;
using Waves.Core.Models;
using Waves.Core.Services;
using Waves.Core.Settings;
using WavesLauncher.Core.Contracts;

namespace KuroGameDownloadProgram.Tests
{
    public static class KuroClientHomeFeedTest
    {
        public static async Task StartTask()
        {
            using var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddGameContext();
                    services.AddSingleton<AppSettings>();
                    services.AddSingleton<IKuroAccountService, KuroAccountService>();
                    services.AddSingleton<IKuroClient, KuroClient>();
                    services.AddKeyedSingleton<LoggerService>(
                        "AppLog",
                        static (_, _) =>
                        {
                            var logger = new LoggerService();
                            logger.InitLogger(AppSettings.LogPath, RollingInterval.Day);
                            return logger;
                        }
                    );
                })
                .Build();
            var accoutService = host.Services.GetRequiredService<IKuroAccountService>();
            await accoutService.SetAutoUser();
            var currentAccount = accoutService.Current;
            var kuroClient = host.Services.GetRequiredService<IKuroClient>();
            var result = await kuroClient.FeedHomeListsAsync(
                KuroAccount.From(currentAccount),
                HomeFeedOption.CreateHomeWaves(Random.Shared.Next(20), 20)
            );
            var postItem = result.Data.PostList[0];
            var sharedResult = await kuroClient.SharedPostIdAsync(
                KuroAccount.From(currentAccount),
                HomeFeedSharedOption.CreateWaves(postItem.PostId)
            );
            var detail = await kuroClient.GetFeedPageDetailAsync(
                KuroAccount.From(currentAccount),
                HomeFeedPostDetailOption.Create(postItem.PostId)
            );
        }
    }
}
