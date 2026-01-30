using Waves.Api.Models;
using Waves.Api.Models.Communitys;
using Waves.Api.Models.Communitys.DataCenter;
using Waves.Api.Models.Communitys.DataCenter.ResourceBrief;
using Waves.Api.Models.GameWikiiClient;
using Waves.Api.Models.QRLogin;
using Waves.Core.Contracts;
using Waves.Core.Models.Enums;

namespace WavesLauncher.Core.Contracts;

public interface IKuroClient
{

    public Task<bool> IsLoginAsync(CancellationToken token = default);

    public IKuroAccountService AccountService { get; }
    public IHttpClientService HttpClientService { get; }

    public Task<GamerDataModel?> GetGamerDataAsync(
        GameRoilDataItem gamerRoil,
        CancellationToken token = default
    );
    public Task<GamerRoil?> GetGamerAsync(GameType gameId, CancellationToken token = default);

    public Task<SMSResultModel?> SendSMSAsync(
        string mobile,
        string geeTestData,
        string tokenDid,
        CancellationToken token = default
    );

    public Task<SignIn?> GetSignInDataAsync(GameRoilDataItem item);

    public Task<AccountModel?> LoginAsync(
        string mobile,
        string code,
        string tokenDid,
        CancellationToken token = default
    );

    public Task<SignRecord?> GetSignRecordAsync(GameRoilDataItem item);
    public Task<SignInResult?> SignInAsync(
        GameRoilDataItem item,
        CancellationToken token = default
    );
    public Task<AccountMine?> GetWavesMineAsync(long id, CancellationToken token = default);

    public Task<AccountMine?> GetWavesMineAsync(long id, string tokenDid,string tokenValue, CancellationToken token = default);

    public Task<ScanScreenModel?> PostQrValueAsync(
        string qrText,
        CancellationToken token = default
    );

    public Task<QRLoginResult?> QRLoginAsync(
        string qrText,
        string verifyCode,
        string id,
        CancellationToken token = default
    );

    public Task<SMSModel?> GetQrCodeAsync(string qrCode, CancellationToken token = default);

    public Task<DeviceInfo?> GetDeviceInfosAsync(CancellationToken token = default);
    public Task<AddUserGameServer?> GetBindServerAsync(
        int gameId,
        CancellationToken token = default
    );

    public Task<SendGameVerifyCode?> SendVerifyGameCode(
        string gameId,
        string serverId,
        string roldId,
        CancellationToken token = default
    );

    public Task<BindGameVerifyCode?> BindGamer(
        string gameId,
        string serverId,
        string roleId,
        string verifyCode,
        CancellationToken token = default
    );

    #region 数据终端
    Task<GamerBassData?> GetGamerBassDataAsync(
        GameRoilDataItem roil,
        CancellationToken token = default
    );
    Task<GamerRoleData?> GetGamerRoleDataAsync(
        GameRoilDataItem roil,
        CancellationToken token = default
    );

    Task<GamerCalabashData?> GetGamerCalabashDataAsync(
        GameRoilDataItem roil,
        CancellationToken token = default
    );

    Task<GamerTowerModel?> GetGamerTowerIndexDataAsync(
        GameRoilDataItem roil,
        CancellationToken token = default
    );

    Task<GamerExploreIndexData?> GetGamerExploreIndexDataAsync(
        GameRoilDataItem roil,
        CancellationToken token = default
    );

    Task<GamerChallengeIndexData?> GetGamerChallengeIndexDataAsync(
        GameRoilDataItem roil,
        CancellationToken token = default
    );

    Task<GamerDataBool?> RefreshGamerDataAsync(
        GameRoilDataItem roil,
        CancellationToken token = default
    );

    Task<GamerRoilDetily?> GetGamerRoilDetily(
        GameRoilDataItem roil,
        long roleId,
        CancellationToken token = default
    );

    Task<GamerChallengeDetily?> GetGamerChallengeDetails(
        GameRoilDataItem roil,
        int countryCode,
        CancellationToken token = default
    );

    Task<GamerSkin?> GetGamerSkinAsync(GameRoilDataItem roil, CancellationToken token = default);

    public Task<GamerSlashDetailData?> GetGamerSlashDetailAsync(
        GameRoilDataItem roil,
        CancellationToken token = default
    );

    Task<BriefHeader?> GetBriefHeaderAsync(CancellationToken token = default);

    public Task<ResourceBrefItem> GetVersionBrefItemAsync(
        string roleId,
        string serverId,
        string versionId,
        CancellationToken token = default
    );
    public Task<ResourceBrefItem> GetWeekBrefItemAsync(
        string roleId,
        string serverId,
        string versionId,
        CancellationToken token = default
    );
    public Task<ResourceBrefItem> GetMonthBrefItemAsync(
        string roleId,
        string serverId,
        string versionId,
        CancellationToken token = default
    );
    #endregion
    public Task<RefreshToken?> UpdateRefreshToken(
        GameRoilDataItem item,
        CancellationToken token = default
    );

    public Task InitAsync();

    #region Wiki
    public Task<WikiHomeModel> GetMainWikiAsync(CancellationToken token = default);
    Task SetAutoUserAsync(CancellationToken token);
    #endregion
}
