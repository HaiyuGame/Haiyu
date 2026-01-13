using MemoryPack;
using System.IO;
using Waves.Api.Models.Rpc;
using Waves.Api.Models.Rpc.CloudGame;
using Waves.Core.Settings;

namespace Haiyu.Services;

public partial class RpcMethodService
{
    /// <summary>
    /// 获取单个单个账号的抽卡令牌
    /// </summary>
    /// <param name="key"></param>
    /// <param name="_param"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<string> GetReocrdTokenAsync(string key, List<RpcParams>? _param = null)
    {
        VerifyToken(_param);
        if (TryGetValue("userName", _param, out var user))
        {
            var UserData = await CloudConfigManager.GetUserAsync(user);
            if (UserData == null)
                throw new ArgumentException("local userName error!");
            var open = await CloudGameService.OpenUserAsync(UserData);
            if (open.Item1)
            {
                var record = await CloudGameService.GetRecordAsync();
                if (record != null)
                {
                    return JsonSerializer.Serialize(record.Data, RpcContext.Default.RecordData);
                }
            }
            else
            {
                throw new ArgumentException(open.Item2);
            }
        }
        throw new ArgumentException("local userName error!");
    }

    /// <summary>
    /// 获取全部账号名称
    /// </summary>
    /// <param name="key"></param>
    /// <param name="_param"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<string> GetCloudAccountsAsync(string key, List<RpcParams>? _param = null)
    {
        VerifyToken(_param);
        var users = await CloudConfigManager.GetUsersAsync().ConfigureAwait(false);
        if (users == null || users.Count == 0)
        {
            throw new ArgumentException("user is null！");
        }
        return JsonSerializer.Serialize(
            users.Select(x => x.Username).ToList(),
            RpcContext.Default.ListString
        );
    }

    /// <summary>
    /// 另存为某个账号的抽卡记录信息
    /// </summary>
    /// <param name="key"></param>
    /// <param name="_param"></param>
    /// <returns></returns>
    public async Task<string> SaveAsCloudRecordResourceAsync(string key, List<RpcParams>? _param = null)
    {
        try
        {
            VerifyToken(_param);
            if (TryGetValue("userName", _param, out var userLoginData))
            {
                var user = await CloudConfigManager.GetUserAsync(userLoginData).ConfigureAwait(false);
                var isLogin = await CloudGameService.OpenUserAsync(user).ConfigureAwait(false);
                if (!isLogin.Item1)
                {
                    throw new RpcException(401, false, "登陆过期，请在客户端重新添加账号");
                }
                var FiveGroup = await RecordHelper.GetFiveGroupAsync().ConfigureAwait(false);
                var AllRole = await RecordHelper.GetAllRoleAsync().ConfigureAwait(false);
                var AllWeapon = await RecordHelper.GetAllWeaponAsync().ConfigureAwait(false);
                var StartRole = RecordHelper.FormatFiveRoleStar(FiveGroup);
                Dictionary<int, IList<RecordCardItemWrapper>> @param =
                    new Dictionary<int, IList<RecordCardItemWrapper>>();
                var url = await CloudGameService.GetRecordAsync();
                if(url.Code != 0)
                {
                    throw new RpcException(401, false, "请求频繁");
                }
                for (int i = 1; i < 10; i++)
                {
                    var player1 = await CloudGameService.GetGameRecordResource(
                        url.Data.RecordId,
                        url.Data.PlayerId.ToString(),
                        i
                    ).ConfigureAwait(false);
                    var WeaponsActivity = player1
                        .Data.Select(x => new RecordCardItemWrapper(x))
                        .ToList();
                    param.Add(i, WeaponsActivity);
                }
                var cache = new RecordCacheDetily()
                {
                    Name = user.Username,
                    Time = DateTime.Now,
                    RoleActivityItems = param[1],
                    WeaponsActivityItems = param[2],
                    RoleResidentItems = param[3],
                    WeaponsResidentItems = param[4],
                    BeginnerItems = param[5],
                    BeginnerChoiceItems = param[6],
                    GratitudeOrientationItems = param[7],
                    RoleJourneyItems = param[8],
                    WeaponJourneyItems = param[9],
                };
                var datas = MemoryPackSerializer.Serialize<RecordCacheDetily>(
                    cache,
                    new MemoryPackSerializerOptions() { StringEncoding = StringEncoding.Utf8 }
                );
                var result = await RecordHelper.MargeRecordAsync(AppSettings.RecordFolder, cache)!.ConfigureAwait(false);
                if (TryGetValue("savePath", _param, out var savePath))
                {
                    if(File.Exists(savePath))
                        File.Delete(savePath);
                    var jsonData = JsonSerializer.Serialize(
                        result.catche,
                        RecordCacheDetilyContext.Default.RecordCacheDetily
                    );
                    using var fs = File.CreateText(savePath);
                    await fs.WriteAsync(jsonData).ConfigureAwait(false);
                    return JsonSerializer.Serialize(
                        new SaveAsReponse()
                        {
                            DataCount = result.Item2,
                            FileSize = result.Item1,
                            MargeTime = DateTime.Now,
                            Path = savePath,
                        },
                        RpcContext.Default.SaveAsReponse
                    );
                }
                else
                {
                    throw new RpcException(401, false, "抽卡合并完成，无另存文件信息");
                }
            }
            else
            {
                throw new RpcException(401,false,"无查询用户");
            }
        }
        catch (RpcException ex)
        {
            throw ex;
        }
        catch (Exception ex)
        {
            throw new RpcException(401, false, ex.Message);
        }
    }
}
