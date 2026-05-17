using System.Collections.Concurrent;
using Waves.Api.Models.CloudGame;
using Waves.Core.Models.CloudGame;

namespace Waves.Core.Services.CloudGameServices;

/// <summary>
/// 账号登录缓存数据
/// </summary>
public class WavesCloudUserCache
{
    private readonly ConcurrentDictionary<string, CloudGameLoginSession> queryList = new();

    public bool IsCheck(CloudGameLoginData data)
    {
        bool flage = false;
        var id = data.Username + data.Sdkuserid;
        if (!queryList.ContainsKey(id))
        {
            flage = false;
            return flage;
        }
        //五小时自动刷新
        if(DateTime.Now- queryList[id].SaveTime>TimeSpan.FromHours(5))
        {
            return false;
        }
        return flage;
    }

    public void TryAdd(CloudGameLoginData data, PhoneTokenData phoneToken =null, AccessData data1 = null, EndLoginReponseData data2 = null)
    {
        var key = data.Username + data.Sdkuserid;
        if (!queryList.ContainsKey(key))
        {
            queryList.TryAdd(key, new CloudGameLoginSession { OrginData = data});
        }
        if(phoneToken != null)
            queryList[key].PhoneToken = phoneToken;
        if(data1 != null)
            queryList[key].AccessData = data1;
        if(data2 != null)
        {
            queryList[key].EndLoginData = data2;
        }
        queryList[key].SaveTime = DateTime.Now;
        queryList[key].TraceId = data.LoginDid;
    }

    public void TryReplace(CloudGameLoginData data)
    {
        var key = data.Username + data.Sdkuserid;
        if (queryList.ContainsKey(key))
        {
            queryList[key] = new CloudGameLoginSession { OrginData = data };
        }
    }

    public CloudGameLoginSession? TryRemove(CloudGameLoginData data)
    {
        var key = data.Username + data.Sdkuserid;
        if (queryList.ContainsKey(key))
        {
            queryList.TryRemove(key, out var result);
            return result;
        }
        return null;
    }

    public CloudGameLoginSession? TryGet(CloudGameLoginData data)
    {
        var key = data.Username + data.Sdkuserid;
        if (queryList.TryGetValue(key, out var cache))
        {
            return cache;
        }
        return null;
    }

    public IEnumerable<CloudGameLoginSession> ToList()
    {
        return queryList.Values.ToList();
    }
}
