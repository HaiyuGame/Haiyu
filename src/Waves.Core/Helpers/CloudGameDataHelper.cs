namespace Waves.Core.Common;

/// <summary>
/// 云游戏构建参数
/// </summary>
public static class CloudGameDataHelper
{
    public const string WelinkTenantKey = "1853717215719854081";
    public const string WelinkGameId = "1853717365355843585600007";
    public const string MainUrl = "https://mc.kurogames.com/cloud/index.html";
    public static void BuildLauncheOption(CloudGameLoginSession login) { }

    public static Dictionary<string, string> BuildWebStorageItems(CloudGameLoginSession login)
    {
        var sdkLoginInfo = new CloudSdkLoginInfo
        {
            Cuid = login.OrginData.Cuid,
            Token = login.AccessData.AccessToken,
            Username = login.OrginData.Username,
            Phone = login.PhoneToken.Phone,
            SdkOpenid = login.PhoneToken.Id,
        };

        var appLoginInfo = new CloudAppLoginInfo
        {
            Token = login.EndLoginData.Token,
            UniqueId = login.EndLoginData.UniqueId,
            WalletData = login.EndLoginData.WalletData,
        };

        var appStore = new CloudGameAppStore
        {
            SdkLoginInfo = sdkLoginInfo,
            AppLoginInfo = appLoginInfo,
        };

        return new Dictionary<string, string>()
        {
            { "wl_cloud_game_userId", login.EndLoginData.UniqueId },
            { $"useMcCloudGameUserSession@Official#{login.OrginData.Id}", "1-" },
            { "sdkuserid", login.OrginData.Sdkuserid },
            { "username", login.OrginData.Username },
            { "cuid", login.OrginData.Cuid },
            { "useMcCloudGameDid", login.OrginData.LoginDid },
            { "welink_cloud_game_uuid", Guid.NewGuid().ToString().ToUpperInvariant() },
            {
                "sdkLoginInfo",
                JsonSerializer.Serialize(sdkLoginInfo, CloudGameContext.Default.CloudSdkLoginInfo)
            },
            { "token", login.EndLoginData.Token },
            { $"McCloudSessionId", Guid.NewGuid().ToString() },
            {
                "useMcCloudGameAppStore",
                JsonSerializer.Serialize(appStore, CloudGameContext.Default.CloudGameAppStore)
            },
            { $"__KrSDK_UUID__", Guid.NewGuid().ToString("N") },
            { $"show_user_name", $"1" },
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
        var autoToken = login.OrginData.AutoToken;
        if (string.IsNullOrWhiteSpace(autoToken))
        {
            autoToken = login.PhoneToken?.AutoToken;
        }

        var phoneToken = login.OrginData.PhoneToken;
        if (string.IsNullOrWhiteSpace(phoneToken))
        {
            phoneToken = login.PhoneToken?.PhoneToken;
        }

        return new Dictionary<string, string>()
        {
            { "token", login.EndLoginData.Token },
            { "autoToken", autoToken },
            { "phoneToken", phoneToken },
            { "username", login.OrginData.Username },
            { "sdkuserid", login.OrginData.Sdkuserid },
            { "cuid", login.OrginData.Cuid },
            { "code", login.OrginData.Code },
        };
    }

    public static BrowserSessionLaunchOptions BuildLaunchOption(
        CloudGameLoginSession session,
        StreamQualityOptions qualityOptions
    )
    {
        return new BrowserSessionLaunchOptions
        {
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
            Quality = qualityOptions,
            StreamDpi = qualityOptions.DPI,
        };
    }

    /// <summary>
    /// 通过DPI缩放转换物理像素
    /// </summary>
    /// <param name="quality"></param>
    /// <param name="clampToWindow"></param>
    /// <returns></returns>
    public static StreamQualityOptions ScaleQualityToPhysical(
        StreamQualityOptions quality,
        bool clampToWindow
    )
    {
        var dpiScale = Math.Max(1.0, quality.DPI / 96.0);
        var isSmooth = quality.Type ==  Models.Enums.CloudQualityType.Smooth;
        var qualityScale = isSmooth ? 2.0 / 3.0 : 1.0;
        var maxWidth = Math.Max(640, (int)Math.Round(quality.Width * qualityScale));
        var maxHeight = Math.Max(360, (int)Math.Round(quality.Height * qualityScale));
        var physicalWidth = ClampEven(
            (int)Math.Round(quality.Width * dpiScale * qualityScale),
            640,
            maxWidth
        );
        var physicalHeight = ClampEven(
            (int)Math.Round(quality.Height * dpiScale * qualityScale),
            360,
            maxHeight
        );
        const double targetBitRatePerPixel = 0.010;
        var targetBitRate = (int)(physicalWidth * physicalHeight * targetBitRatePerPixel);
        targetBitRate = Math.Clamp(
            targetBitRate,
            CloudGameMethod.MinBitRate,
            CloudGameMethod.DefaultBitRate
        );
        return new StreamQualityOptions(
            targetBitRate,
            quality.BitRateMin,
            quality.Fps,
            physicalWidth,
            physicalHeight,
            quality.CodecType,
            quality.StreamStrategy,
            quality.EnableImageEnhancement,
            quality.DPI,
            quality.Type
        );
    }

    public static HttpClient CreateWebCloudClient(CloudGameLoginSession session)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip
                | DecompressionMethods.Deflate
                | DecompressionMethods.Brotli,
            UseCookies = false,
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://cloud-game-sh.aki-game.com/"),
        };

        client.DefaultRequestHeaders.Add("Kr-Ver", "1.9.0");
        client.DefaultRequestHeaders.Add("x-token", session.EndLoginData.Token);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9");
        client.DefaultRequestHeaders.Referrer = new Uri(
            "https://mc.kurogames.com/cloud/index.html"
        );
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Code/1.120.0 Chrome/142.0.7444.265 Electron/39.8.8 Safari/537.36"
        );
        client.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://mc.kurogames.com");
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Cookie",
            string.Join(
                ";",
                CloudGameDataHelper
                    .BuildCookieItems(session)
                    .Select(pair => $"{pair.Key}={pair.Value}")
            )
        );
        client.DefaultRequestHeaders.TryAddWithoutValidation("x-os", "web");
        client.DefaultRequestHeaders.Add("x-b3-traceid",session.TraceId);
        return client;
    }

    public static CloudBizData CreateCloudBizData(IEnumerable<BizCloudNode> nodes)
    {
        return new CloudBizData(
            "Code/1.120.0 Chrome/142.0.7444.265 Electron/39.8.8 Safari/537.36",
            CloudGameMethod.WelinkClientVersion,
            nodes
        );
    }

    public static WelinkStartParameters CreateWebLinkParameters(
        int dpi,
        int maxWidth,
        int maxHeight,
        int bitRate,
        int fps,
        int codeType,
        string bizData,
        IEnumerable<CloudGameNode> nodes,
        CloudGameNode node
    )
    {
        return new WelinkStartParameters(
            WelinkTenantKey,
            WelinkGameId,
            GetPreferredResolution(dpi, maxWidth, maxHeight),
            bitRate,
            fps,
            codeType,
            "v1.0",
            $"-CloudGamePlatform=Windows -fps={fps} -Dpi={dpi} -DeviceScreenResolution={GetPreferredResolution(dpi, maxWidth, maxHeight)} -Device=Windows -SkipSplash -IsWeb=1",
            bizData,
            nodes,
            node
        );
    }

    private static string GetPreferredResolution(int dpi, int maxWidth, int maxHeight)
    {
        var screenWidth = ClampEven(
            maxWidth,
            CloudGameMethod.MinStreamWidth,
            CloudGameMethod.MaxStreamWidth
        );
        var screenHeight = ClampEven(
            maxHeight,
            CloudGameMethod.MinStreamHeight,
            CloudGameMethod.MaxStreamHeight
        );

        return $"{screenWidth}x{screenHeight}";
    }

    private static int ClampEven(int value, int minValue, int maxValue)
    {
        var clamped = Math.Max(minValue, Math.Min(maxValue, value));
        return clamped % 2 == 0 ? clamped : Math.Max(minValue, clamped - 1);
    }
}
