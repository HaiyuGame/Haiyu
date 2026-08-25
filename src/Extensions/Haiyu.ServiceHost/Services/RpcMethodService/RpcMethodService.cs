using System.Security.Cryptography;
using System.Text;
using Haiyu.ServiceHost.Contracts;
using Waves.Api.Models.Rpc;
using Waves.Core.Common;
using Waves.Core.Contracts;
using Waves.Settings;
using Haiyu.KuroClient;
using Waves.Core.Contracts.CloudGame;

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
    /// <summary>
    /// KuroClient 支持的方法名称
    /// </summary>
    kuro_methods = 3,
    /// <summary>
    /// 调用 KuroClient 方法
    /// </summary>
    kuro_call = 4,
    account_list = 5,
    account_current = 6,
    cloud_account_list = 7,
    cloud_account_select = 8,
    cloud_gacha_record_info = 9,
    cloud_gacha_records = 10,
}

public partial class RpcMethodService : IRpcMethodService
{
    public RpcMethodService(
        IKuroClient kuroClient,
        CloudConfigManager cloudConfigManager,
        AppSettings appSettings,
        RpcSettings rpcSettings,
        IKuroAccountService kuroAccountService,
        IWavesCloudGameService wavesCloudGameService
    )
    {
        KuroClient = kuroClient;
        CloudConfigManager = cloudConfigManager;
        AppSettings = appSettings;
        RpcSettings = rpcSettings;
        KuroAccountService = kuroAccountService;
        WavesCloudGameService = wavesCloudGameService;
    }

    public IKuroClient KuroClient { get; }
    public CloudConfigManager CloudConfigManager { get; }
    public AppSettings AppSettings { get; }
    public RpcSettings RpcSettings { get; }
    public IKuroAccountService KuroAccountService { get; }
    public IWavesCloudGameService WavesCloudGameService { get; }

    public Dictionary<string, Func<string, List<RpcParams>, Task<string>>> Method =>
        new Dictionary<string, Func<string, List<RpcParams>, Task<string>>>()
        {
            { nameof(RpcMethodKey.app_ping), PingAsync },
            { nameof(RpcMethodKey.app_version), GetRpcVersionAsync },
            { nameof(RpcMethodKey.app_methods),GetRpcMethodsAsync }
            ,{ nameof(RpcMethodKey.kuro_methods), GetKuroClientMethodsAsync }
            ,{ nameof(RpcMethodKey.kuro_call), CallKuroClientAsync }
            ,{ nameof(RpcMethodKey.account_list), GetLocalAccountsAsync }
            ,{ nameof(RpcMethodKey.account_current), GetCurrentAccountAsync }
            ,{ nameof(RpcMethodKey.cloud_account_list), GetCloudAccountsAsync }
            ,{ nameof(RpcMethodKey.cloud_account_select), SelectCloudAccountAsync }
            ,{ nameof(RpcMethodKey.cloud_gacha_record_info), GetCloudGachaRecordInfoAsync }
            ,{ nameof(RpcMethodKey.cloud_gacha_records), GetCloudGachaRecordsAsync }
        };

    public async Task<string> PingAsync(string key, List<RpcParams>? _param = null)
    {
        return "0";
    }

    public bool VerifyToken(List<RpcParams>? rpcParams = null)
    {
        if (!TryGetValue("token", rpcParams, out var token) || string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Verification failed");

        var configuredTokenHash = RpcSettings.GetAuthTokenAsync().GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(configuredTokenHash))
            throw new ArgumentException("Verification failed");

        var suppliedTokenHash = Md5Helper.ComputeMd532(token);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredTokenHash.ToUpperInvariant());
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedTokenHash.ToUpperInvariant());
        if (!CryptographicOperations.FixedTimeEquals(configuredBytes, suppliedBytes))
            throw new ArgumentException("Verification failed");

        return true;
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
