using System.Net.Http.Json;
using System.Web;
using Haiyu.Plugin.Contracts;
using Haiyu.Plugin.Services;
using MemoryPack;
using Waves.Api.Helper;
using Waves.Api.Importing;
using Waves.Api.Exporting;
using Waves.Api.Models.CloudGame;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.GameContext.ContextsV2.Waves;
using ZLinq;

namespace Haiyu.Services;

/// <summary>
/// 使用V3版本，此类专门为游戏核心创建一个档案入口，云鸣潮档案入口后续迁移至此
/// </summary>
public class WavesRecordManager
{
    public IWavesPlayerCardCacheServices CacheService { get; set; }
    public IWavesCloudGameService WavesCloudGameService { get; }

    public WavesRecordManager(
        [FromKeyedServices($"{nameof(WavesPlayerCardCacheServices)}V3")]
            IWavesPlayerCardCacheServices wavesPlayerCardCacheServices,
        IWavesCloudGameService wavesCloudGameService
    )
    {
        this.CacheService = wavesPlayerCardCacheServices;
        WavesCloudGameService = wavesCloudGameService;
    }

    public string DefaultRecordUrl => "https://gmserver-api.aki-game2.com/gacha/record/query";

    public string GlobalRecordUrl => "https://gmserver-api.aki-game2.net/gacha/record/query";

    /// <summary>
    /// 离线文件档案入口
    /// </summary>
    /// <param name="request">本地文件请求创建</param>
    /// <returns></returns>
    public async Task CreateDefaultRecordAsync(
        CreateWavesRecordRequest request,
        CancellationToken token = default
    )
    {
        var querys = FormatUrl(request.RecordLink);
        var url =
            request.GameContextName == nameof(WavesGlobalGameContextV2)
                ? GlobalRecordUrl
                : DefaultRecordUrl;
        using HttpClient hClient = new();
        var recordId = querys["record_id"];
        var playerId = querys["player_id"];
        var resourceId = querys["resources_id"];
        var serverId = querys["svr_id"];
        var lang = querys["lang"];
        ArgumentNullException.ThrowIfNull(recordId);
        ArgumentNullException.ThrowIfNull(playerId);
        ArgumentNullException.ThrowIfNull(resourceId);
        ArgumentNullException.ThrowIfNull(serverId);
        ArgumentNullException.ThrowIfNull(lang);
        WavesAnalysisPlayerCard? cards = await CacheService.LoadAsync(playerId);
        if (cards != null)
        {
            cards.Items = new List<WavesAnalysisPlayerCardItem>();
        }
        else
        {
            cards = new()
            {
                Items = new List<WavesAnalysisPlayerCardItem>(),
                LastUpdater = DateTime.Now,
                SessionId = playerId.ToString(),
                RecordName = request.RecordName,
            };
        }
        cards.Items = [];
        foreach (var item in CardPoolTypeValues.All)
        {
            PlayerCard payload = new PlayerCard()
            {
                CardPoolId = resourceId,
                CardPoolType = (int)item,
                PlayerId = playerId,
                ServerId = serverId,
                RecordId = recordId,
                LanguageCode = lang,
            };
            var httpRequest = new HttpRequestMessage()
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload, CommunityContext.Default.PlayerCard),
                    new MediaTypeHeaderValue("application/json")
                ),
                Method = HttpMethod.Post,
                RequestUri = new(DefaultRecordUrl),
            };
            var reponse = await hClient.SendAsync(httpRequest);
            var result = await reponse.Content.ReadFromJsonAsync<PlayerReponse>(
                CommunityContext.Default.PlayerReponse
            );
            if (result?.Data != null)
            {
                cards.Items.Add(
                    new WavesAnalysisPlayerCardItem()
                    {
                        PoolType = (int)item,
                        Resource = result.Data.Select(x => new RecordCardItemWrapper(x)),
                    }
                );
            }
        }
        cards.SessionId = playerId;
        cards.UpdateType = "GameContext";
        cards.UpdateSource = $"{playerId}-{request.GameContextName}";
        cards.Version = 2;
        cards.RecordName = request.RecordName;
        await CacheService.SaveAsync(cards);
    }

    /// <summary>
    /// 云鸣潮档案入口
    /// </summary>
    /// <param name="session">云鸣潮登录信息</param>
    /// <returns></returns>
    public async Task CreateCloudRecordAsync(
        CloudGameLoginSession session,
        string recordName,
        CancellationToken token = default
    )
    {
        var record = await WavesCloudGameService.GetRecordAsync(session);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(record.Data);
        WavesAnalysisPlayerCard cards = await CacheService.LoadAsync(
            record.Data.PlayerId.ToString()
        );

        if (cards != null)
        {
            cards.Items = new List<WavesAnalysisPlayerCardItem>();
        }
        else
        {
            cards = new()
            {
                Items = new List<WavesAnalysisPlayerCardItem>(),
                LastUpdater = DateTime.Now,
                SessionId = record.Data.PlayerId.ToString(),
                RecordName = recordName,
            };
        }
        foreach (var item in CardPoolTypeValues.All)
        {
            var resources = await this.WavesCloudGameService.GetGameRecordResource(
                session,
                record.Data.RecordId,
                record.Data.PlayerId.ToString(),
                (int)item
            );
            if (resources?.Data != null)
            {
                cards.Items.Add(
                    new WavesAnalysisPlayerCardItem()
                    {
                        PoolType = (int)item,
                        Resource = resources.Data.Select(x => new RecordCardItemWrapper(x)),
                    }
                );
            }
        }
        await this.CacheService.SaveAsync(cards);
    }

    public async Task<List<WavesAnalysisPlayerCard>> GetLocalWavesRecordsAsync(
        CancellationToken token = default
    )
    {
        return await CacheService.LoadAllAsync();
    }

    public async Task<bool> ImportRecordCardsAsync(string filePath, string targetSessionId)
    {
        var preview = await PreviewRecordImportAsync(filePath, targetSessionId);
        await MergeRecordCardsAsync(preview.Card, targetSessionId);
        return true;
    }

    public async Task<(int Archives, int Records)> ExportAllRecordsAsync(
        string filePath, CancellationToken token = default)
    {
        var cards = await CacheService.LoadAllAsync();
        return await Task.Run(() => WavesRecordExcelExporter.Write(filePath, cards, token), token);
    }

    public async Task<(
        WavesAnalysisPlayerCard Card,
        int Total,
        int Duplicates,
        double Percent
    )> PreviewRecordImportAsync(string filePath, string targetSessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSessionId);
        if (
            !string.Equals(
                Path.GetExtension(filePath),
                ".record",
                StringComparison.OrdinalIgnoreCase
            )
        )
            throw new InvalidDataException("请选择 .record 抽卡记录文件。");

        var imported = MemoryPackSerializer.Deserialize<WavesAnalysisPlayerCard>(
            await File.ReadAllBytesAsync(filePath),
            new MemoryPackSerializerOptions { StringEncoding = StringEncoding.Utf8 }
        );
        return await PreviewImportedRecordAsync(imported, targetSessionId);
    }

    public async Task<(
        WavesAnalysisPlayerCard Card,
        int Total,
        int Duplicates,
        double Percent
    )> PreviewExcelImportAsync(
        string filePath,
        string targetSessionId,
        CancellationToken token = default
    )
    {
        var imported = await Task.Run(
            () => WutheringWavesToolExcelReader.Read(filePath, token),
            token
        );
        return await PreviewImportedRecordAsync(imported, targetSessionId);
    }

    private async Task<(
        WavesAnalysisPlayerCard Card,
        int Total,
        int Duplicates,
        double Percent
    )> PreviewImportedRecordAsync(WavesAnalysisPlayerCard? imported, string targetSessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSessionId);
        var target =
            await CacheService.LoadAsync(targetSessionId)
            ?? throw new InvalidOperationException("目标档案已不存在，请刷新后重新选择。");
        if (imported?.Items == null || !imported.Items.Any(x => x?.Resource?.Any() == true))
            throw new InvalidDataException("所选文件中没有有效的抽卡记录。");

        if (
            imported.Items.Any(x =>
                x == null || x.Resource == null || x.Resource.Any(r => r == null)
            )
        )
            throw new InvalidDataException("所选文件包含损坏的卡池或抽卡数据。");

        // 旧文件可能多次写入相同卡池，先归并为每个卡池一项。
        imported.Items = imported
            .Items.GroupBy(x => x.PoolType)
            .Select(group => group.Aggregate((left, right) => left.Merge(right)))
            .ToList();
        // 和保存时使用相同的合并规则，确保预计新增条数与实际一致。
        return await Task.Run(() =>
        {
            var total = 0;
            var duplicates = 0;
            foreach (var pool in imported.Items)
            {
                var existing = target.Items.FirstOrDefault(x => x.PoolType == pool.PoolType);
                var incomingCount = pool.Resource.Count();
                var existingCount = existing?.Resource?.Count() ?? 0;
                var mergedCount = (existing == null ? pool : existing.Merge(pool)).Resource.Count();
                total += incomingCount;
                duplicates += incomingCount + existingCount - mergedCount;
            }
            return (imported, total, duplicates, total == 0 ? 0d : duplicates * 100d / total);
        });
    }

    public async Task MergeRecordCardsAsync(
        WavesAnalysisPlayerCard imported,
        string targetSessionId
    )
    {
        var target =
            await CacheService.LoadAsync(targetSessionId)
            ?? throw new InvalidOperationException("目标档案已不存在，请刷新后重新选择。");

        // 只导入抽卡数据；目标档案的标识、名称及更新来源保持原样。
        // SaveAsync 会按卡池合并现有记录并去重，不依赖源文件的 SessionId。
        target.Items = imported
            .Items.Select(pool =>
            {
                var existing = target.Items.FirstOrDefault(x => x.PoolType == pool.PoolType);
                return existing == null ? pool : existing.Merge(pool);
            })
            .ToList();
        await CacheService.SaveAsync(target);
    }

    private NameValueCollection FormatUrl(string url)
    {
        var linkUri = new System.Uri(url);
        var query = linkUri.Fragment.Split('?', 2);
        return HttpUtility.ParseQueryString(query.Length > 1 ? query[1] : "");
    }
}

public class CreateWavesRecordRequest
{
    public string RecordName { get; set; }

    public string RecordLink { get; set; }

    public string GameContextName { get; set; }
}
