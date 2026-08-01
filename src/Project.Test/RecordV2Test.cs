using Haiyu.Plugin.Contracts;
using Haiyu.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Waves.Api.Models.Enums;
using Waves.Api.Models.Record;
using Waves.Core;
using Waves.Core.Contracts;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.Services;
using Waves.Core.Services.CloudGameServices;
using Waves.Settings;

namespace Project.Test;

[TestClass]
public class RecordV2Test
{
    public IServiceProvider Provider { get; private set; }

    [TestMethod]
    public async Task RecordTestSave()
    {
        var data = await RefreshDataAsync();
        
    }

    private async Task<WavesAnalysisPlayerCard> RefreshDataAsync()
    {
        Directory.CreateDirectory(AppSettings.WavesRecordFolder);
        var context = await InitServiceAsync();
        await Task.Delay(2000);
        var cache = Provider.GetService<IWavesPlayerCardCacheServices>();
        var sessions = context.WavesCloudSurivivalService.Cache.ToList();
        var current = sessions.FirstOrDefault();
        ArgumentNullException.ThrowIfNull(current);
        var recordId =
            await context.WavesCloudSurivivalService.WavesCloudGameService.GetRecordAsync(current);
        ArgumentNullException.ThrowIfNull(recordId);
        ArgumentNullException.ThrowIfNull(recordId.Data);
        WavesAnalysisPlayerCard cards = new();
        cards.SessionId = current.GetId();
        cards.LastUpdater = DateTime.Now;
        cards.Items = new List<WavesAnalysisPlayerCardItem>();
        foreach (var item in CardPoolTypeValues.All)
        {
            var resources =
                await context.WavesCloudSurivivalService.WavesCloudGameService.GetGameRecordResource(
                    current,
                    recordId.Data.RecordId,
                    recordId.Data.PlayerId.ToString(),
                    (int) item
                );
            if (resources?.Data != null)
            {
                cards.Items.Add(
                    new WavesAnalysisPlayerCardItem()
                    {
                        PoolType = (int) item,
                        Resource = resources.Data.Select(
                            x => new Waves.Api.Models.Wrappers.RecordCardItemWrapper(x)
                        ),
                    }
                );
            }
        }
        return await cache.SaveAsync(cards);
    }

    private async Task<IKuroCloudGameContext> InitServiceAsync()
    {
        Provider = new ServiceCollection()
            .AddSingleton<IWavesPlayerCardCacheServices>(_ => new WavesPlayerCardCacheServices(
                AppSettings.WavesRecordFolder
            ))
            .AddSingleton<IWavesCloudGameService, WavesCloudGameService>()
            .AddSingleton<WavesCloudSurvivalService>()
            .AddSingleton<CloudConfigManager>(
                (s) =>
                {
                    var mananger = new CloudConfigManager(AppSettings.CloudFolderPath);
                    return mananger;
                }
            )
            .AddSingleton<AppSettings>()
            .AddGameContext()
            .BuildServiceProvider();
        var context = Provider.GetKeyedService<IKuroCloudGameContext>(nameof(KuroCloudGameContext));
        ArgumentNullException.ThrowIfNull(context);
        await context.InitAsync();
        await Provider.GetService<WavesCloudSurvivalService>()!.StartAsync();
        return context;
    }
}
