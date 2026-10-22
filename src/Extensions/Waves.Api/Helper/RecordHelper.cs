
using System.Text.Json;
using MemoryPack;
using Waves.Api.Models;
using Waves.Api.Models.Enums;
using Waves.Api.Models.Record;
using Waves.Api.Models.Wrappers;

namespace Waves.Api.Helper;

public static class RecordHelper
{
    public static HttpRequestMessage BuildRequets(RecordRequest recordRequest, CardPoolType type)
    {
        HttpRequestMessage message = new()
        {
            RequestUri = new Uri($"https://gmserver-api.aki-game2.com/gacha/record/query"),
            Method = HttpMethod.Post
        };
        recordRequest.CardPoolType = (int)type;
        var str = new StringContent(
            JsonSerializer.Serialize(recordRequest, PlayerCardRecordContext.Default.RecordRequest),
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json", "UTF-8")
        );
        message.Content = str;
        return message;
    }

    public static RecordRequest? GetRecorRequest(string uri)
    {
        try
        {
            RecordRequest request = new();
            var str = uri.Split('?')[1];
            var dic = str.Split('&').Select(x => x.Split('=')).ToDictionary(x => x[0], x => x[1]);
            request.PlayerId = dic["player_id"];
            request.CardPoolId = dic["resources_id"];
            request.ServerId = dic["svr_id"];
            request.Language = dic["lang"];
            request.RecordId = dic["record_id"];
            return request;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task<List<RecordCardItemWrapper>?> GetRecordAsync(
        RecordRequest recordRequest,
        CardPoolType type
    )
    {
        try
        {
            var message = BuildRequets(recordRequest, type);
            var client = new HttpClient();
            var response = await client.SendAsync(message);
            response.EnsureSuccessStatusCode();
            var model = JsonSerializer.Deserialize(
                await response.Content.ReadAsStringAsync(),
                PlayerCardRecordContext.Default.PlayerReponse
            );
            List<RecordCardItemWrapper> items = new();
            if (model == null)
                return null;
            if (model != null && model.Code == -1)
                return null;
            if (model != null && model.Code == 0)
            {
                items = model.Data.Select(x => new RecordCardItemWrapper(x)).ToList();
            }
            return items;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    /// <summary>
    /// 根据角色 ID 离线判断是否为常驻五星。
    /// </summary>
    public static bool IsResident(int id) => id is 1301 or 1203 or 1503 or 1104 or 1405;

    public static (List<Tuple<RecordCardItemWrapper, int, bool?>>?, int?) FormatStartFive(
        IEnumerable<RecordCardItemWrapper> source,
        out int lastCount,
        bool checkResident = false
    )
    {
        List<Tuple<RecordCardItemWrapper, int, bool?>> result = new();
        int count = 0;
        var items = source.Reverse();
        if (!checkResident)
        {
            foreach (var item in items)
            {
                if (item.QualityLevel == 5)
                {
                    result.Add(new(item, count, null));
                    count = 0;
                }
                else
                {
                    count++;
                }
            }
            lastCount = 0;
            return (result, count);
        }
        foreach (var item in items)
        {
            if (item.QualityLevel == 5)
            {
                result.Add(new(item, count, IsResident(item.ResourceId)));
                count = 0;
            }
            else
            {
                count++;
            }
            
        }
        if (count > 0)
        {
            lastCount = count;
        }
        else
        {
            lastCount = 0;
        }

        return (result, count);
    }


    public static List<Tuple<RecordCardItemWrapper, int>> FormatRecordFive(
        IEnumerable<RecordCardItemWrapper> source
    )
    {
        List<Tuple<RecordCardItemWrapper, int>> result = new();
        int count = 1;
        foreach (var item in source.Reverse())
        {
            if (item.QualityLevel == 5)
            {
                result.Add(new(item, count));
                count = 1;
            }
            else
            {
                count++;
            }
        }
        return result;
    }

    public static List<int> FormatFiveRoleStar(FiveGroupModel model) =>
        model.Data.VersionPools.SelectMany(x => x.UpFiveRoleIds).ToList();

    public static List<int> FormatFiveWeaponeRoleStar(FiveGroupModel model) =>
        model.Data.VersionPools.SelectMany(x => x.UpFiveWeaponIds).ToList();

    public static async Task<FiveGroupModel?> GetFiveGroupAsync(CancellationToken token = default)
    {
        using (HttpClient client = new HttpClient())
        {
            try
            {
                var response = await client.GetAsync(
                    "https://api3.sanyueqi.cn/api/v1/pool/draw_config_infos", token
                );
                response.EnsureSuccessStatusCode();
                var model = JsonSerializer.Deserialize(
                    await response.Content.ReadAsStringAsync(token),
                    PlayerCardRecordContext.Default.FiveGroupModel
                );
                return model;
            }
            catch (Exception)
            {
                throw new ArgumentException("获取限定分组错误");
            }
        }
    }

    public static async Task<List<CommunityRoleData>?> GetAllRoleAsync(CancellationToken token= default)
    {
        using (HttpClient client = new HttpClient())
        {
            try
            {
                var response = await client.GetAsync("https://mc.appfeng.com/json/avatar.json", token);
                response.EnsureSuccessStatusCode();
                var model = JsonSerializer.Deserialize(
                    await response.Content.ReadAsStringAsync(token),
                    PlayerCardRecordContext.Default.ListCommunityRoleData
                );
                if (model != null)
                    return model;
                return null;
            }
            catch (Exception)
            {
                throw new ArgumentException("获取全部角色错误");
            }
        }
    }

    public static async Task<List<CommunityWeaponData>?> GetAllWeaponAsync(CancellationToken token =default)
    {
        using (HttpClient client = new HttpClient())
        {
            try
            {
                var response = await client.GetAsync("https://mc.appfeng.com/json/weapon.json?", token);
                response.EnsureSuccessStatusCode();
                var model = JsonSerializer.Deserialize(
                    await response.Content.ReadAsStringAsync(token),
                    PlayerCardRecordContext.Default.ListCommunityWeaponData
                );
                if (model != null)
                    return model;
                return null;
            }
            catch (Exception)
            {
                throw new ArgumentException("获取全部武器错误");
            }
        }
    }

    /// <summary>
    /// 计算小保底歪率
    /// </summary>
    /// <param name="itemWrapper"></param>
    /// <returns></returns>
    public static double GetGuaranteedRange(
        this IEnumerable<Tuple<RecordCardItemWrapper, int, bool?>> itemWrapper
    )
    {
        if (itemWrapper == null || !itemWrapper.Any())
        {
            return 0;
        }
        // 反转后按抽卡时间从旧到新遍历（若原列表是新到旧排列，反转后为旧到新，符合推导顺序）
        var items = itemWrapper.ToList();
        int totalSmallGuarantees = 0;
        int totalSmallGuaranteeFails = 0;
        // 初始状态：第一次五星为小保底（isNextSmallGuarantee=true表示下一个五星是小保底）
        bool isNextSmallGuarantee = true;

        foreach (var item in items)
        {
            var isOffBanner = item.Item3;
            if (isOffBanner.HasValue)
            {
                // 仅当当前五星是小保底状态时，计入统计
                if (isNextSmallGuarantee)
                {
                    totalSmallGuarantees++;
                    if (isOffBanner.Value)
                    {
                        totalSmallGuaranteeFails++;
                    }
                }
                // 根据当前五星结果，更新下一个五星的保底状态
                // 若当前歪了（isOffBanner=true），下一个是大保底；若没歪（false），下一个是小保底
                isNextSmallGuarantee = !isOffBanner.Value;
            }
        }

        if (totalSmallGuarantees == 0)
        {
            return 0;
        }

        return (double)totalSmallGuaranteeFails / totalSmallGuarantees * 100;
    }

    public static double CalculateAvg(this IEnumerable<Tuple<RecordCardItemWrapper, int>> value) =>
        value.Average(x => x.Item2);

    /// <summary>
    /// 计算此样本的分数
    /// </summary>
    /// <param name="guaranteedRange">小保底歪率</param>
    /// <param name="roleAAvg">活动角色平均抽数</param>
    /// <param name="weaponAAvg">活动武器平均抽数</param>
    /// <param name="roleIAvg">常驻角色平均抽数</param>
    /// <param name="weaponIAvg">常驻武器平均抽数</param>
    /// <returns></returns>
    public static double Score(
        double guaranteedRange,
        double roleAAvg,
        double weaponAAvg,
        double resident
    )
    {
        double weight1 = 0.40;
        double weight2 = 0.2;
        double weight3 = 0.2;
        double weight4 = 0.2;
        double minScore1 = 0;
        double maxScore1 = 100;
        double minScore2 = 0;
        double maxScore2 = 80;
        double minScore3 = 0;
        double maxScore3 = 80;
        double minScore4 = 0;
        double maxScore4 = 80;
        double weightedScore1 =
            (1 - ((guaranteedRange - minScore1) / (maxScore1 - minScore1))) * weight1;
        double weightedScore2 = (1 - ((roleAAvg - minScore2) / (maxScore2 - minScore2))) * weight2;
        double weightedScore3 = (1 - ((weaponAAvg - minScore3) / (maxScore3 - minScore3))) * weight3;
        double weightedScore4 = (1 - ((resident - minScore4) / (maxScore4 - minScore4))) * weight4;
        double totalScore =
            (weightedScore1 + weightedScore2 + weightedScore3 + weightedScore4) * 100;
        return totalScore;
    }

    /// <summary>
    /// 获得垫了几发,以及最近获得的5星
    /// </summary>
    /// <param name="recordCardItems"></param>
    /// <returns></returns>
    public static Tuple<RecordCardItemWrapper, int> GetAdvanceData(
        IEnumerable<RecordCardItemWrapper> recordCardItems
    )
    {
        RecordCardItemWrapper wrapper = null;
        int count = 0;
        foreach (var item in recordCardItems)
        {
            if (item.QualityLevel == 5)
            {
                wrapper = item;
                break;
            }
            else
            {
                count++;
            }
        }
        return new(wrapper, count);
    }

    public static async Task<(long, long, RecordCacheDetily? catche)>? MargeRecordAsync(
        string baseFolder,
        RecordCacheDetily cache
    )
    {
        try
        {
            var cachePath = "";
            RecordCacheDetily? localRecord = null;
            foreach (
                var item in Directory.GetFiles(baseFolder, "*.json", SearchOption.TopDirectoryOnly)
            )
            {
                var data = MemoryPackSerializer.Deserialize<RecordCacheDetily>(
                    await File.ReadAllBytesAsync(item),
                    new MemoryPackSerializerOptions() { StringEncoding = StringEncoding.Utf8 }
                );
                if (data == null)
                    continue;
                if (data.Name == cache.Name)
                {
                    localRecord = data;
                    cachePath = item;
                }
            }
            if (string.IsNullOrWhiteSpace(cachePath))
            {
                cachePath = baseFolder + $"\\{cache.Name}.json";
                File.Create(cachePath).Dispose();
            }
            var sortedCache = SortItemsByTime(cache);
            var sortedLocal = SortItemsByTime(localRecord);
            var mergedRecord = new RecordCacheDetily
            {
                Name = cache.Name,
                Time = cache.Time,
                RoleJourneyItems = SyncCompareLists(
                    sortedCache.RoleJourneyItems,
                    sortedLocal.RoleJourneyItems
                ),
                WeaponJourneyItems = SyncCompareLists(
                    sortedCache.WeaponJourneyItems,
                    sortedLocal.WeaponJourneyItems
                ),
                RoleActivityItems = SyncCompareLists(
                        sortedCache.RoleActivityItems,
                        sortedLocal.RoleActivityItems
                    )
                    .OrderByDescending(x => x.RecordTime)
                    .ToList(),
                RoleResidentItems = SyncCompareLists(
                        sortedCache.RoleResidentItems,
                        sortedLocal.RoleResidentItems
                    )
                    .OrderByDescending(x => x.RecordTime)
                    .ToList(),
                WeaponsActivityItems = SyncCompareLists(
                        sortedCache.WeaponsActivityItems,
                        sortedLocal.WeaponsActivityItems
                    )
                    .OrderByDescending(x => x.RecordTime)
                    .ToList(),
                WeaponsResidentItems = SyncCompareLists(
                        sortedCache.WeaponsResidentItems,
                        sortedLocal.WeaponsResidentItems
                    )
                    .OrderByDescending(x => x.RecordTime)
                    .ToList(),
                BeginnerChoiceItems = SyncCompareLists(
                        sortedCache.BeginnerChoiceItems,
                        sortedLocal.BeginnerChoiceItems
                    )
                    .OrderByDescending(x => x.RecordTime)
                    .ToList(),
                BeginnerItems = SyncCompareLists(sortedCache.BeginnerItems, sortedLocal.BeginnerItems)
                    .OrderByDescending(x => x.RecordTime)
                    .ToList(),
                GratitudeOrientationItems = SyncCompareLists(
                        sortedCache.GratitudeOrientationItems,
                        sortedLocal.GratitudeOrientationItems
                    )
                    .OrderByDescending(x => x.RecordTime)
                    .ToList(),
            };
            var byteData = MemoryPackSerializer.Serialize<RecordCacheDetily>(
                mergedRecord,
                new MemoryPackSerializerOptions() { StringEncoding = StringEncoding.Utf8 }
            );
            await File.WriteAllBytesAsync(cachePath, byteData);
            return (
                byteData.Length,
                mergedRecord
                    .RoleActivityItems.Concat(mergedRecord.WeaponsActivityItems)
                    .Concat(mergedRecord.RoleResidentItems)
                    .Concat(mergedRecord.WeaponsResidentItems)
                    .Concat(mergedRecord.BeginnerItems)
                    .Concat(mergedRecord.BeginnerChoiceItems)
                    .Concat(mergedRecord.GratitudeOrientationItems)
                    .Concat(mergedRecord.RoleJourneyItems)
                    .Concat(mergedRecord.WeaponJourneyItems)
                    .Count()
            , mergedRecord);
        }
        catch (Exception)
        {
            return (0, 0, null);
        }
        
    }

    public static RecordCacheDetily? SortItemsByTime(RecordCacheDetily recordCache)
    {
        if (recordCache == null)
        {
            return new RecordCacheDetily() { Name = "" };
        }
        recordCache.RoleActivityItems = recordCache
            .RoleActivityItems.OrderByDescending(x => x.RecordTime)
            .ToList();
        recordCache.RoleResidentItems = recordCache
            .RoleResidentItems.OrderByDescending(x => x.RecordTime)
            .ToList();
        recordCache.WeaponsActivityItems = recordCache
            .WeaponsActivityItems.OrderByDescending(x => x.RecordTime)
            .ToList();
        recordCache.WeaponsResidentItems = recordCache
            .WeaponsResidentItems.OrderByDescending(x => x.RecordTime)
            .ToList();
        recordCache.BeginnerChoiceItems = recordCache
            .BeginnerChoiceItems.OrderByDescending(x => x.RecordTime)
            .ToList();
        recordCache.BeginnerItems = recordCache
            .BeginnerItems.OrderByDescending(x => x.RecordTime)
            .ToList();
        recordCache.GratitudeOrientationItems = recordCache
            .GratitudeOrientationItems.OrderByDescending(x => x.RecordTime)
            .ToList();

        return recordCache;
    }

    public static List<RecordCardItemWrapper> SyncCompareLists(
     IList<RecordCardItemWrapper> oldRecords,
     IList<RecordCardItemWrapper> newRecords)
    {
        var safeOldRecords = oldRecords ?? new List<RecordCardItemWrapper>();
        var safeNewRecords = newRecords ?? new List<RecordCardItemWrapper>();

        var mergedResult = new List<RecordCardItemWrapper>();

        mergedResult.AddRange(safeOldRecords);

        foreach (var newItem in safeNewRecords)
        {
            bool isExistInOld = safeOldRecords.Any(oldItem => AreItemsEqual(oldItem, newItem));
            if (!isExistInOld)
            {
                mergedResult.Add(newItem);
            }
        }

        return mergedResult;
    }

    private static bool AreItemsEqual(RecordCardItemWrapper item1, RecordCardItemWrapper item2)
    {
        return item1.Time == item2.Time
            && item1.CardPoolType == item2.CardPoolType
            && item1.ResourceId == item2.ResourceId
            && item1.QualityLevel == item2.QualityLevel
            && item1.ResourceType == item2.ResourceType
            && item1.Name == item2.Name
            && item1.Count == item2.Count;
    }
}
