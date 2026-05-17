using Waves.Api.Models.CloudGame;

namespace Waves.Core.Contracts;

/// <summary>
/// 云鸣潮接口会话
/// </summary>
public interface ICloudGameService
{
    public CloudConfigManager ConfigManager { get; }

    public Task<CloudSendSMS> GetPhoneSMSAsync(
        string phone,
        string geetestCaptchaOutput,
        string geetestPassToken,
        string geetestGenTime,
        string geetestLotNumber,
        CancellationToken token = default
    );

    public void SetLoginData(CloudGameLoginData data);

    Task<RecordModel> GetRecordAsync(CancellationToken token = default);
    Task<PlayerReponse> GetGameRecordResource(string recordId, string userId, int poolType, CancellationToken token = default);
    /// <summary>
    /// 创建连接会话
    /// </summary>
    /// <param name="loginData"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<(bool, string)> OpenUserAsync(CloudGameLoginData loginData, CancellationToken token = default);
}
