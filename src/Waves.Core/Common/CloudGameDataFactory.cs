using System.Net.NetworkInformation;
using System.Text.Json;
using Waves.Api.Models;
using Waves.Api.Models.CloudGame;
using Waves.Core.Models.CloudGame;

namespace Waves.Core.Common;

/// <summary>
/// 云游戏构建参数
/// </summary>
public static class CloudGameDataFactory
{
    public const string MainUrl = "https://mc.kurogames.com/cloud/index.html";

    public static void BuildLauncheOption(CloudGameLoginSession login) { }

    public static Dictionary<string, string> BuildWebStorageItems(CloudGameLoginSession login)
    {
        return new Dictionary<string, string>()
        {
            { "wl_cloud_game_userId", login.EndLoginData.UniqueId },
            { $"useMcCloudGameUserSession@Official#{login.OrginData.Id}", "1-" },
            { "sdkuserid", login.OrginData.Sdkuserid },
            { "username", login.OrginData.Username },
            { "cuid", login.OrginData.Cuid },
            { "useMcCloudGameDid", login.OrginData.LoginDid },
            { "refreshToken", login.PhoneToken.PhoneToken },
            { "welink_cloud_game_uuid", Guid.NewGuid().ToString().ToUpperInvariant() },
            {
                "sdkLoginInfo",
                JsonSerializer.Serialize(
                    login.OrginData,
                    CloudGameContext.Default.CloudGameLoginData
                )
            },
            { "token", login.EndLoginData.Token },
            { $"McCloudSessionId", Guid.NewGuid().ToString() },
            {
                "useMcCloudGameAppStore",
                JsonSerializer.Serialize(login.EndLoginData, CloudGameContext.Default.EndLoginData)
            },
            { $"__KrSDK_UUID__", Guid.NewGuid().ToString("N") },
            { $"show_user_name", $"1" },
            { $"wl_cloud_game_userId", login.EndLoginData.UniqueId },
            { "code", login.OrginData.Code },
            { "autoToken", login.OrginData.AutoToken },
            { "phoneToken", login.OrginData.PhoneToken },
            { "refreshToken", login.PhoneToken.PhoneToken },
            { "accessToken", login.AccessData.AccessToken },
            { "access_token", login.AccessData.AccessToken },
            { "refresh_token", login.PhoneToken.PhoneToken },
            {
                "__KrSDK_SYS_CONF__",
                """
{"thirdLogin":{"wechat":{"enabled":0},"qq":{"enabled":0},"taptap":{"enabled":0},"phone":{"enabled":1},"phoneQk":{"enabled":0},"tourist":{"enabled":0},"accLogin":{"enabled":0},"accReg":{"enabled":0},"apple":{"enabled":0},"qrLogin":{"enabled":0}},"heartFreq":5,"heartEnable":1,"uagr":"https://wutheringwaves.kurogame.com/p/agreement_public.html","pagr":"https://wutheringwaves.kurogame.com/p/personal_privacy.html","childArgUrl":"https://wutheringwaves.kurogame.com/p/child_privacy.html","webViewUrlWhiteList":["www.kurogame.com"],"clientUrl":{"accCenterUrl":"https://pro-cdn-sdk.kurogame.com/pro/sdk_web/index.html#/account_center","cancelUrl":"https://pro-cdn-sdk.kurogame.com/pro/sdk_personal/account-cancellation.html","sobot":"https://web-static.kurogame.com/shared/zhichi-cs/index.html","kefuServ":"https://user-zone-api.kurogame.com/notice/","kefu":"https://web-static.kurogame.com/shared/cs/index.html","pcGeetestUrl":"https://pro-cdn-sdk.kurogame.com/pro/sdk_personal/geetest.html","sobotRedDotUrl":"https://user-zone-api.kurogame.com/notice/sobot/client/red-dot"},"pwdLife":"60d","clientSwitch":{"disableWebviewVideoSupportedForWin":false,"sobot":true,"marketTencent":true,"marketToutiao":true,"ksCps":true,"useNewClientFocus":false,"krData":true,"clientFocus":true,"uaOsVersionForWin":true,"td":true,"bytedance":false,"webAccelerated":true,"trackingioPayUpload":true,"kefu":true,"appleASA":true,"didGt":false,"trackingioPayMoneyUpload":true},"toSwitch":true,"kefuInterval":300,"kefuSessionExpireHour":1,"accessTokenCheckFreq":10,"didSmSwitch":false,"didTxSwitch":false,"geetestSwitch":true,"cancelSwitch":false}
"""
            },
            { "__KrSDK__", BuildKrSdkJson(login) },
            { "G152", BuildPkgData(login) },
        };
    }

    private static string BuildKrSdkJson(CloudGameLoginSession session)
    {
        KRSDK sdk = new KRSDK()
        {
            Lang = "zh-Hans",
            GameId = "G152",
            PkgId = "A1493",
            Pkg = "com.kurogame.mingchao",
            ChannelId = "211",
            ClientId = "vvkewnskrxxwfo0yi61cy24l",
            ClientSecret = "g9ej0i1jf3y68wchb0ncm266",
            ViewType = string.Empty,
            RoleData = new(),
            LoginConfig = new LoginConfig()
            {
                Age = session.PhoneToken.Age,
                AutoToken = session.PhoneToken.AutoToken,
                AutoTokenStatus = session.PhoneToken.AutoTokenStatus,
                BindDevStat = 0,
                Code = session.PhoneToken.Code,
                Cuid = session.PhoneToken.Cuid,
                FirstLgn = session.PhoneToken.FirstLgn,
                Id = session.PhoneToken.Id,
                IdStat = session.PhoneToken.IdStat,
                LoginType = session.PhoneToken.LoginType,
                Phone = session.PhoneToken.Phone,
                PhoneCheck = session.PhoneToken.PhoneCheck,
                PhoneToken = session.PhoneToken.PhoneToken,
                SdkOpenid = session.PhoneToken.Id,
                Sdkuserid = session.PhoneToken.Sdkuserid,
                ShowPaw = session.PhoneToken.ShowPaw,
                Token = session.AccessData.AccessToken,
                Username = session.PhoneToken.Username,
            },
        };
        return JsonSerializer.Serialize(sdk, CloudGameContext.Default.KRSDK);
    }

    public static string BuildPkgData(CloudGameLoginSession session)
    {
        PKGData data = new PKGData()
        {
            Age = 22,
            AutoToken = session.PhoneToken.AutoToken,
            AutoTokenStatus = session.PhoneToken.AutoTokenStatus,
            BindDevStat = session.PhoneToken.BindDevStat,
            Code = session.PhoneToken.Code,
            Cuid = session.PhoneToken.Cuid,
            FirstLgn = session.PhoneToken.FirstLgn,
            Id = session.PhoneToken.Id,
            IdStat = session.PhoneToken.IdStat,
            LoginType = session.PhoneToken.LoginType,
            Phone = session.PhoneToken.Phone,
            PhoneCheck = session.PhoneToken.PhoneCheck,
            PhoneToken = session.PhoneToken.PhoneToken,
            SdkOpenid = session.PhoneToken.Id,
            Sdkuserid = session.PhoneToken.Sdkuserid,
            ShowPaw = session.PhoneToken.ShowPaw,
            Token = session.AccessData.AccessToken,
            Username = session.PhoneToken.Username,
        };
        return JsonSerializer.Serialize(data, CloudGameContext.Default.PKGData);
    }

    public static Dictionary<string, string> BuildCookieItems(CloudGameLoginSession login)
    {
        return new Dictionary<string, string>()
        {
            { "token", login.AccessData.AccessToken },
            { "autoToken", login.OrginData.AutoToken },
            { "phoneToken", login.OrginData.PhoneToken },
            { "username", login.OrginData.Username },
            { "sdkuserid", login.OrginData.Sdkuserid },
            { "cuid", login.OrginData.Cuid },
            { "code", login.OrginData.Code },
        };
    }

    public static SessionLaunchOptions BuildLaunchOption(CloudGameLoginSession session)
    {
        return new SessionLaunchOptions
        {
            GameUrl = "https://mc.kurogames.com/cloud/index.html",
            BootstrapUrl = "https://mc.kurogames.com/cloud/index.html",
            AccessToken = session.AccessData.AccessToken,
            RefreshToken = session.PhoneToken.PhoneToken,
            CookieDomain = ".kurogames.com",
            AdditionalHeaders = new Dictionary<string, string>(),
            Cookies = BuildCookieItems(session),
            StorageItems = BuildWebStorageItems(session),
            HeaderHostPatterns =
            [
                "mc.kurogames.com",
                "cloud-game-sh.aki-game.com",
                "usercenter.kurogames.com",
                "*.aki-game.com",
                "*.kurogames.com",
            ],
            Quality = new StreamQualityOptions(18000, 8000, 60, 1920, 1080, 21, "0", true, "clear")
            
        };
    }
}
