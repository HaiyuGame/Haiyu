using Waves.Api.Models.KuroClient;
using Waves.Api.Models.KuroClient.Options;

namespace WavesLauncher.Core.Contracts;

public interface IKuroClient
{
    IHttpClientService HttpClientService { get; }

    Task<bool> IsLoginAsync(KuroAccount account, CancellationToken token = default);
    Task<GamerDataModel?> GetGamerDataAsync(
        KuroAccount account,
        GameRoilDataItem role,
        CancellationToken token = default
    );
    Task<GamerRoil?> GetGamerAsync(
        KuroAccount account,
        GameType gameId,
        CancellationToken token = default
    );

    Task<SMSResultModel?> SendSMSAsync(
        string mobile,
        string geeTestData,
        string tokenDid,
        CancellationToken token = default
    );
    Task<AccountModel?> LoginAsync(
        string mobile,
        string code,
        string tokenDid,
        CancellationToken token = default
    );
    Task<SignIn?> GetSignInDataAsync(KuroAccount account, GameRoilDataItem item);
    Task<SignRecord?> GetSignRecordAsync(KuroAccount account, GameRoilDataItem item);
    Task<SignInResult?> SignInAsync(
        KuroAccount account,
        GameRoilDataItem item,
        CancellationToken token = default
    );
    Task<AccountMine?> GetWavesMineAsync(
        KuroAccount account,
        long id,
        CancellationToken token = default
    );

    Task<ScanScreenModel?> PostQrValueAsync(
        KuroAccount account,
        string qrText,
        CancellationToken token = default
    );
    Task<QRLoginResult?> QRLoginAsync(
        KuroAccount account,
        string qrText,
        string verifyCode,
        string id,
        CancellationToken token = default
    );
    Task<SMSModel?> GetQrCodeAsync(
        KuroAccount account,
        string qrCode,
        CancellationToken token = default
    );

    Task<DeviceInfo?> GetDeviceInfosAsync(KuroAccount account, CancellationToken token = default);
    Task<AddUserGameServer?> GetBindServerAsync(
        KuroAccount account,
        int gameId,
        CancellationToken token = default
    );
    Task<SendGameVerifyCode?> SendVerifyGameCode(
        KuroAccount account,
        string gameId,
        string serverId,
        string roldId,
        CancellationToken token = default
    );
    Task<BindGameVerifyCode?> BindGamer(
        KuroAccount account,
        string gameId,
        string serverId,
        string roleId,
        string verifyCode,
        CancellationToken token = default
    );

    Task<GamerBassData?> GetGamerBassDataAsync(
        KuroAccount account,
        GameRoilDataItem role,
        CancellationToken token = default
    );
    Task<GamerRoleData?> GetGamerRoleDataAsync(
        KuroAccount account,
        GameRoilDataItem role,
        CancellationToken token = default
    );
    Task<GamerCalabashData?> GetGamerCalabashDataAsync(
        KuroAccount account,
        GameRoilDataItem role,
        CancellationToken token = default
    );
    Task<GamerTowerModel?> GetGamerTowerIndexDataAsync(
        KuroAccount account,
        GameRoilDataItem role,
        CancellationToken token = default
    );
    Task<GamerExploreIndexData?> GetGamerExploreIndexDataAsync(
        KuroAccount account,
        GameRoilDataItem role,
        CancellationToken token = default
    );
    Task<GamerChallengeIndexData?> GetGamerChallengeIndexDataAsync(
        KuroAccount account,
        GameRoilDataItem role,
        CancellationToken token = default
    );
    Task<GamerDataBool?> RefreshGamerDataAsync(
        KuroAccount account,
        GameRoilDataItem role,
        CancellationToken token = default
    );
    Task<GamerRoilDetily?> GetGamerRoilDetily(
        KuroAccount account,
        GameRoilDataItem role,
        long roleId,
        CancellationToken token = default
    );
    Task<GamerChallengeDetily?> GetGamerChallengeDetails(
        KuroAccount account,
        GameRoilDataItem role,
        int countryCode,
        CancellationToken token = default
    );
    Task<GamerSkin?> GetGamerSkinAsync(
        KuroAccount account,
        GameRoilDataItem role,
        CancellationToken token = default
    );
    Task<GamerSlashDetailData?> GetGamerSlashDetailAsync(
        KuroAccount account,
        GameRoilDataItem role,
        CancellationToken token = default
    );
    Task<BriefHeader?> GetBriefHeaderAsync(KuroAccount account, CancellationToken token = default);
    Task<ResourceBrefItem> GetVersionBrefItemAsync(
        KuroAccount account,
        string roleId,
        string serverId,
        string versionId,
        CancellationToken token = default
    );
    Task<ResourceBrefItem> GetWeekBrefItemAsync(
        KuroAccount account,
        string roleId,
        string serverId,
        string versionId,
        CancellationToken token = default
    );
    Task<ResourceBrefItem> GetMonthBrefItemAsync(
        KuroAccount account,
        string roleId,
        string serverId,
        string versionId,
        CancellationToken token = default
    );
    Task<RefreshToken?> UpdateRefreshToken(
        KuroAccount account,
        GameRoilDataItem item,
        CancellationToken token = default
    );

    Task InitAsync();
    Task<WikiHomeModel> GetMainWikiAsync(KuroAccount account, CancellationToken token = default);
    Task InitMapPostion(KuroAccount account);

    Task<KuroClientReturnCode<KuroClientSignInModel>?> SignInClientAsync(
        KuroAccount account,
        CancellationToken cts = default
    );

    Task<KuroClientReturnCode<KuroClientHomeFeedModel>?> FeedHomeListsAsync(
        KuroAccount account,
        HomeFeedOption option,
        CancellationToken cts = default
    );

    Task<KuroClientReturnCode<bool>?> PostIdLikeAsync(
        KuroAccount account,
        HomeFeedLikeOption option,
        CancellationToken token = default
    );

    Task<KuroClientReturnCode<bool>?> SharedPostIdAsync(
        KuroAccount account,
        HomeFeedSharedOption option,
        CancellationToken token = default
    );

    Task<KuroClientReturnCode<KuroClientPostPageDetail>?> GetFeedPageDetailAsync(
        KuroAccount account,
        HomeFeedPostDetailOption option,
        CancellationToken token = default
    );

    Task<KuroClientReturnCode<KuroEncourageProcessModel>?> GetEncourageProcessAsync(
        KuroAccount account,
        EncourageProcessOption option,
        CancellationToken token = default
    );
    Task<KuroClientReturnCode<EncourageTotalGoldModel>?> GetEncourageTotalGoldAsync(
        KuroAccount account,
        CancellationToken token = default
    );
}
