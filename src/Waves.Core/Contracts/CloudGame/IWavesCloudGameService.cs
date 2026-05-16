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

    public Task<LoginResult?> LoginAsync(
        CloudGameLoginSnapshot snapshot,
        string phone,
        string code,
        CancellationToken token = default
    );
}
