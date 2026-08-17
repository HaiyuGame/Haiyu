using System.Security.Cryptography;
using Haiyu.ServiceHost.Contracts;
using Waves.Api.Models.Rpc;
using Waves.Core.Common;
using Waves.Core.Contracts;
using Waves.Settings;
using Haiyu.KuroClient;

namespace Haiyu.ServiceHost.Services;

public enum RpcMethodKey:int
{
    /// <summary>
    /// 检查APP是否响应
    /// </summary>
    app_ping = 0,
    /// <summary>
    /// 检查App Rpc版本
    /// </summary>
    app_version = 1,
    /// <summary>
    /// RPC协议支持接口名称
    /// </summary>
    app_methods = 2,
}

public partial class RpcMethodService : IRpcMethodService
{
    public RpcMethodService(IKuroClient kuroClient ,CloudConfigManager cloudConfigManager, AppSettings appSettings)
    {
        KuroClient = kuroClient;
        CloudConfigManager = cloudConfigManager;
        AppSettings = appSettings;
    }

    public IKuroClient KuroClient { get; }
    public CloudConfigManager CloudConfigManager { get; }
    public AppSettings AppSettings { get; }

    public Dictionary<string, Func<string, List<RpcParams>, Task<string>>> Method =>
        new Dictionary<string, Func<string, List<RpcParams>, Task<string>>>()
        {
            { nameof(RpcMethodKey.app_ping), PingAsync },
            { nameof(RpcMethodKey.app_version), GetRpcVersionAsync },
            { nameof(RpcMethodKey.app_methods),GetRpcMethodsAsync }
        };

    public async Task<string> PingAsync(string key, List<RpcParams>? _param = null)
    {
        return "0";
    }

    public bool VerifyToken(List<RpcParams>? rpcParams = null)
    {
        MD5 md5 = MD5.Create();
        try
        {
            if(TryGetValue("token",rpcParams,out var token))
            {
                if (AppSettings.GetRpcTokenAsync().GetAwaiter().GetResult() != Md5Helper.ComputeMd532(token))
                {
                    throw new ArgumentException("Verification failed");
                }
                else
                {
                    return false;
                }
            }
            return true;
        }
        catch (Exception)
        {
            throw new ArgumentException("Verification failed");
        }
        finally
        {
            md5.Dispose();
        }
    }

    /// <summary>
    /// 检查获取参数
    /// </summary>
    /// <param name="key"></param>
    /// <param name="rpcParams"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public bool TryGetValue(string key, List<RpcParams>? rpcParams, out string? value)
    {
        try
        {
            if (rpcParams == null)
            {
                value = null;
                throw new ArgumentException("Verification failed");
            }
            var token = rpcParams.FirstOrDefault(x => x.Key == key)?.Value;
            if (string.IsNullOrWhiteSpace(token))
            {
                value = null;
                throw new ArgumentException("Verification failed");
            }
            value = token;
            return true;
        }
        catch (Exception)
        {
            value = null;
            return false;
        }
    }

    /// <summary>
    /// 检查获取多参数
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="rpcParams"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    public bool TryGetValues(IList<string> keys, List<RpcParams>? rpcParams, out List<string?> values)
    {
        List<string?> result = [];
        try
        {
            foreach (var item in keys)
            {
                if (TryGetValue(item, rpcParams, out var value))
                {
                    result.Add(value);
                }
            }
            values = result;
            return true;
        }
        catch (Exception ex)
        {
            values = null;
            return false;
        }
    }

    
}
