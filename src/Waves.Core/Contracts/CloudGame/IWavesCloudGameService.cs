using Waves.Api.Models.CloudGame;
using Waves.Core.Models.CloudGame;

namespace Waves.Core.Contracts.CloudGame;

public interface IWavesCloudGameService
{
    public CloudConfigManager ConfigManager { get; }
    public Task<Tuple<CloudSendSMS?, CloudGameLoginSnapshot>> GetPhoneSMSAsync(
        string phone,
        string geetestCaptchaOutput,
        string geetestPassToken,
        string geetestGenTime,
        string geetestLotNumber,
        CancellationToken token = default
    );

    public Task<CloudApiResponse<CloudGameLoginData>?> LoginAsync(
       CloudGameLoginSnapshot snapshot,
       string phone,
       string code,
       CancellationToken token = default
   );

    public Task<CloudApiResponse<PhoneTokenData>?> RefreshPhoneTokenAsync(
        CloudGameLoginData data,
        CancellationToken ct = default
    );

    public Task<CloudApiResponse<AccessData>?> GetAccessToken(
        CloudGameLoginData data,
        string refreshPhoneToken,
        CancellationToken ct = default
    );

    public Task<CloudApiResponse<EndLoginReponseData>?> GetTokenAsync(
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
    public Task<CloudApiResponse<bool>> FetchMesageAsync(CloudGameLoginSession session, CancellationToken ct = default);
}
