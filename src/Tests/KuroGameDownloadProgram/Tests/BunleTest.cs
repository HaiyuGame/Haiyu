using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Waves.Api.Models;
using Waves.Core;
using Waves.Core.Contracts;
using Waves.Core.GameContext;
using Waves.Core.GameContext.ContextsV2.Waves;
using Waves.Core.Services.GameResourceProvider;
using Waves.Settings;

namespace KuroGameDownloadProgram.Tests
{
    public static class BunleTest
    {
        public static async Task StartTaskAsync()
        {
            GameContextFactory.GameBassPath =
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Waves";

            IHost host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddGameContext();
                    services.AddSingleton<AppSettings>();
                })
                .Build();
            var appSettings = host.Services.GetService<AppSettings>();
            var a = await appSettings.GetPunishAutoOpenContextAsync();
            var v2 = host.Services.GetRequiredKeyedService<IGameContextV2>(
                nameof(WavesMainGameContextV2)
            );
            await v2.InitAsync();
            Dictionary<string, Tuple<BundleResourcePack, GameLauncherBundle>> dictValue = [];
            if(v2.GameResourceProvider is BundleGameResourceProvider pp)
            {
                var r = await pp.GetBunlesAsync();
            }

            Console.WriteLine();
        }
    }
}
