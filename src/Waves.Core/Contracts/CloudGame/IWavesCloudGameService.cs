namespace Waves.Core.Contracts.CloudGame;

public interface IWavesCloudGameService
{
    CloudConfigManager ConfigManager { get; }

    CloudNetworkSpeedTestService CloudNetworkSpeedTestService { get; }

    Task<Tuple<CloudSendSMS?, CloudGameLoginSnapshot>> GetPhoneSMSAsync(
        string phone,
        CancellationToken token = default
    );

    Task<CloudApiResponse<CloudGameLoginData>?> LoginAsync(
        CloudGameLoginSnapshot snapshot,
        string phone,
        string code,
        CancellationToken token = default
    );

    Task<CloudApiResponse<PhoneTokenData>?> RefreshPhoneTokenAsync(
        CloudGameLoginData data,
        CancellationToken ct = default
    );

    Task<CloudApiResponse<AccessData>?> GetAccessToken(
        CloudGameLoginData data,
        string refreshPhoneToken,
        CancellationToken ct = default
    );

    Task<CloudApiResponse<EndLoginData>?> GetTokenAsync(
        CloudGameLoginData data,
        string accessToken,
        CancellationToken ct = default
    );

    /// <summary>
    /// 保活消息
    /// </summary>
    /// <param name="session"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<CloudApiResponse<bool?>> FetchMesageAsync(
        CloudGameLoginSession session,
        CancellationToken ct = default
    );

    /// <summary>
    /// 云鸣潮计费
    /// </summary>
    /// <param name="session"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<CloudApiResponse<WalletData>?> GetWalletDataAsync(
        CloudGameLoginSession session,
        CancellationToken ct = default
    );

    /// <summary>
    /// 获取节点
    /// </summary>
    /// <param name="session"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<CloudApiResponse<List<CloudGameNode>>?> GetPingGameNodeAsync(
        CloudGameLoginSession session,
        CancellationToken ct = default
    );

    /// <summary>
    /// 开始排队
    /// </summary>
    /// <param name="client"></param>
    /// <param name="session"></param>
    /// <param name="startParameters"></param>
    /// <param name="payType"></param>
    /// <returns></returns>
    Task<CloudApiResponse<CommStartReponse>?> CommonStartGameAsync(
        HttpClient client,
        CloudGameLoginSession session,
        WelinkStartParameters startParameters,
        uint payType
    );

    /// <summary>
    /// 取消排队
    /// </summary>
    /// <param name="client"></param>
    /// <param name="session"></param>
    /// <returns></returns>
    Task CancelQueqeAsync(HttpClient client, CloudGameLoginSession session);

    /// <summary>
    /// 排队信息
    /// </summary>
    /// <param name="client"></param>
    /// <param name="session"></param>
    /// <returns></returns>
    Task<CloudApiResponse<CommonQueueInfo>?> CommonQueueInfoAsync(
        HttpClient client,
        CloudGameLoginSession session
    );

    /// <summary>
    /// 获取抽卡ID
    /// </summary>
    /// <param name="session"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<CloudApiResponse<RecordData>?> GetRecordAsync(
        CloudGameLoginSession session,
        CancellationToken token = default
    );

    /// <summary>
    /// 获取抽卡信息
    /// </summary>
    /// <param name="session"></param>
    /// <param name="recordId"></param>
    /// <param name="userId"></param>
    /// <param name="poolType"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<PlayerReponse?> GetGameRecordResource(
        CloudGameLoginSession session,
        string recordId,
        string userId,
        int poolType,
        CancellationToken token = default
    );
}
