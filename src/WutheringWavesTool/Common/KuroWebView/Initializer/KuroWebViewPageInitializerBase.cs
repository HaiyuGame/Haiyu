using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;
using Haiyu.Common.KuroWebView;

namespace Haiyu.Common.KuroWebView.Initializer
{

    public abstract class KuroWebViewPageInitializerBase : IKuroWebViewPageInitializer
    {
        protected const string AndroidAppUserAgent = "Mozilla/5.0 (Linux; Android 13; 23049RAD8C Build/TQ3A.230805.001; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/124.0.0.0 Mobile Safari/537.36 Kuro/3.1.2 KuroGameBox/3.1.2";
        private static readonly Uri KuroWebStaticOrigin = new("https://web-static.kurobbs.com/");
        private static readonly Uri KuroBbsOrigin = new("https://www.kurobbs.com/");

        private string? _documentScriptId;
        private WebSessionContext _currentSession;
        private CoreWebView2? _hookedCoreWebView2;

        public abstract bool CanInitialize(WebSessionContext session);

        public async Task InitializeAsync(WebView2 webView, WebSessionContext session)
        {
            _currentSession = session;

            await WebView2EnvironmentProvider.EnsureInitializedAsync(webView);
            webView.CoreWebView2.Settings.UserAgent = AndroidAppUserAgent;
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            webView.CoreWebView2.Settings.IsZoomControlEnabled = true;
            HookEvents(webView);
            await InstallSessionScriptAsync(webView, session);
            ApplyCookieSession(webView, session);
        }

        public async Task ApplySessionAsync(WebView2 webView, WebSessionContext session)
        {
            _currentSession = session;

            if (webView.CoreWebView2 is null)
            {
                throw new InvalidOperationException(LanguageService.GetStringByText("WebView2 尚未初始化。"));
            }

            ApplyCookieSession(webView, session);
            await webView.CoreWebView2.ExecuteScriptAsync(BuildApplySessionScript(session));
        }

        protected abstract Dictionary<string, object?> CreateStorageItems(WebSessionContext session);

        protected virtual Dictionary<string, object?> CreateHostEnvironment(WebSessionContext session)
        {
            return new(StringComparer.Ordinal)
            {
                ["platform"] = "android",
                ["appName"] = session.UserAgentName
            };
        }

        protected static bool PageUrlContains(WebSessionContext session, string value)
        {
            return session.PageUrl.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        protected static Dictionary<string, object?> CreateUserInfo(WebSessionContext session)
        {
            return new(StringComparer.Ordinal)
            {
                ["appVersion"] = session.AppVersion,
                ["os"] = session.Os,
                ["headUrl"] = session.HeadUrl,
                ["userName"] = session.UserName,
                ["ua"] = session.UserAgentName,
                ["userId"] = session.UserId,
                ["did"] = session.Did,
                ["channelId"] = session.ChannelId,
                ["enterSource"] = session.EnterSource,
                ["token"] = session.Token
            };
        }

        protected static Dictionary<string, object?> CreateRoleInfo(WebSessionContext session)
        {
            return new(StringComparer.Ordinal)
            {
                ["userId"] = session.UserId,
                ["gameId"] = session.GameId,
                ["serverId"] = session.ServerId,
                ["serverName"] = session.ServerName,
                ["roleId"] = session.RoleId,
                ["roleName"] = session.RoleName,
                ["token"] = session.Token
            };
        }

        protected static Dictionary<string, object?> CreateCompactRoleInfo(WebSessionContext session)
        {
            return new(StringComparer.Ordinal)
            {
                ["gameId"] = session.GameId.ToString(),
                ["roleId"] = session.RoleId,
                ["roleName"] = session.RoleName,
                ["serverName"] = session.ServerName,
                ["userId"] = session.UserId,
                ["serverId"] = session.ServerId,
                ["token"] = session.Token
            };
        }

        private async Task InstallSessionScriptAsync(WebView2 webView, WebSessionContext session)
        {
            if (webView.CoreWebView2 is null)
            {
                throw new InvalidOperationException(LanguageService.GetStringByText("WebView2 尚未初始化。"));
            }

            if (!string.IsNullOrWhiteSpace(_documentScriptId))
            {
                return;
            }

            _documentScriptId = await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BuildBootstrapScript(session));
        }

        private void HookEvents(WebView2 webView)
        {
            CoreWebView2? coreWebView2 = webView.CoreWebView2;
            if (coreWebView2 is null || ReferenceEquals(_hookedCoreWebView2, coreWebView2))
            {
                return;
            }

            if (_hookedCoreWebView2 is not null)
            {
                _hookedCoreWebView2.NavigationCompleted -= OnNavigationCompleted;
            }

            coreWebView2.NavigationCompleted += OnNavigationCompleted;
            _hookedCoreWebView2 = coreWebView2;
        }

        private async void OnNavigationCompleted(
            CoreWebView2 sender,
            CoreWebView2NavigationCompletedEventArgs args
        )
        {
            await sender.ExecuteScriptAsync(BuildApplySessionScript(_currentSession));
        }

        private string BuildBootstrapScript(WebSessionContext session)
        {
            string storageJson = JsonSerializer.Serialize(CreateStorageItems(session), KuroSessionContext.Default.DictionaryStringObject);
            string hostAuthJson = JsonSerializer.Serialize(CreateHostAuth(session), KuroSessionContext.Default.DictionaryStringObject);
            string hostEnvJson = JsonSerializer.Serialize(CreateHostEnvironment(session), KuroSessionContext.Default.DictionaryStringObject);

            return $$"""
            (() => {
                const storageItems = {{storageJson}};
                const hostAuth = {{hostAuthJson}};
                const hostEnv = {{hostEnvJson}};

                const writeValue = (storage, key, value) => {
                    if (!storage || value === undefined || value === null) {
                        return;
                    }

                    try {
                        storage.setItem(key, typeof value === 'string' ? value : JSON.stringify(value));
                    } catch {
                    }
                };

                const applySession = () => {
                    for (const storage of [window.localStorage, window.sessionStorage]) {
                        for (const [key, value] of Object.entries(storageItems)) {
                            writeValue(storage, key, value);
                        }
                    }

                    window.__HOST_AUTH__ = { ...(window.__HOST_AUTH__ || {}), ...hostAuth };
                    window.__KURO_HOST_ENV__ = hostEnv;
                };

                const ok = (payload) => JSON.stringify(payload ?? {});
                const createResponse = (handlerName, data) => {
                    applySession();
                    switch (handlerName) {
                        case 'getUserInfo':
                            return ok(storageItems.initUserInfo || storageItems['aki-calendar:user-info'] || storageItems.AKI_MAP_USER_INFO || hostAuth);
                        case 'getAuthToken':
                        case 'getAccessToken':
                            return ok({ token: hostAuth.token, did: hostAuth.did, userId: hostAuth.userId });
                        case 'refreshToken':
                        case 'refreshTokenV2':
                            return ok({ code: 0, token: hostAuth.token, did: hostAuth.did, userId: hostAuth.userId });
                        case 'getSystemStatus':
                            return ok({ darkMode: false, theme: 'light' });
                        case 'getToolInfo':
                            return ok({ gameId: hostAuth.gameId, roleId: hostAuth.roleId, serverId: hostAuth.serverId });
                        case 'getUserDefaults':
                        case 'getCacheData':
                            return ok({});
                        case 'setToolInfo':
                        case 'toSkip':
                        case 'appShare':
                        case 'finishPage':
                        case 'useSystemSetting':
                        case 'setNavigationBarHidden':
                        case 'setKrStatusBar':
                        case 'setScreenOrientation':
                        case 'setAppNavbarText':
                        case 'setAppShareData':
                        case 'showWidgetGuide':
                            return ok({ result: true });
                        default:
                            return ok({ result: true, echo: data ?? null, handlerName });
                    }
                };

                const bridge = window.WebViewJavascriptBridge || {};
                bridge.inited = true;
                bridge.init = bridge.init || (() => {});
                bridge.registerHandler = bridge.registerHandler || function (handlerName, callback) {
                    this._handlers = this._handlers || {};
                    this._handlers[handlerName] = callback;
                };
                bridge.callHandler = bridge.callHandler || function (handlerName, data, callback) {
                    const response = createResponse(handlerName, data);
                    if (typeof callback === 'function') {
                        setTimeout(() => callback(response), 0);
                    }
                };

                for (const methodName of [
                    'KJQKeyboardChange', 'activityChangeTitle', 'appLogout', 'appShare', 'appShareFailed',
                    'appShareSuccess', 'bindAccount', 'cacheData', 'changeAddress', 'chooseActivityPhoto',
                    'choosePhoto', 'chooseRole', 'finishPage', 'getAccessToken', 'getAuthToken',
                    'getCacheData', 'getSystemStatus', 'getToolInfo', 'getUserDefaults', 'getUserInfo',
                    'listenerKJQKeyboardChange', 'onTitleChanged', 'openGame', 'refreshToken',
                    'refreshTokenV2', 'response', 'selectRole', 'selected', 'send', 'setAppNavbarText',
                    'setAppShareData', 'setKrStatusBar', 'setNavigationBarHidden', 'setScreenOrientation',
                    'setToolInfo', 'share', 'showWidgetGuide', 'toSkip', 'updateTitle', 'useSystemSetting'
                ]) {
                    bridge[methodName] = bridge[methodName] || ((data, callback) => {
                        const response = createResponse(methodName, data);
                        if (typeof callback === 'function') {
                            setTimeout(() => callback(response), 0);
                        }
                        return response;
                    });
                }

                window.WebViewJavascriptBridge = bridge;
                window.jsBridge = window.jsBridge || {
                    utils: {},
                    pcBridge: {},
                    platform: 'android',
                    platformEnum: { android: 'android', ios: 'ios', pc: 'pc' }
                };
                window.WVJBCallbacks = window.WVJBCallbacks || [];
                applySession();

                setTimeout(() => {
                    document.dispatchEvent(new Event('WebViewJavascriptBridgeReady'));
                }, 0);
            })();
            """;
        }

        private string BuildApplySessionScript(WebSessionContext session)
        {
            string storageJson = JavaScriptEncoder.Default.Encode(JsonSerializer.Serialize(CreateStorageItems(session), KuroSessionContext.Default.DictionaryStringObject));
            string hostAuthJson = JavaScriptEncoder.Default.Encode(JsonSerializer.Serialize(CreateHostAuth(session), KuroSessionContext.Default.DictionaryStringObject));
            string hostEnvJson = JavaScriptEncoder.Default.Encode(JsonSerializer.Serialize(CreateHostEnvironment(session), KuroSessionContext.Default.DictionaryStringObject));

            return $$"""
            (() => {
                const storageItems = JSON.parse('{{storageJson}}');
                const hostAuth = JSON.parse('{{hostAuthJson}}');
                const hostEnv = JSON.parse('{{hostEnvJson}}');
                const writeValue = (storage, key, value) => {
                    if (!storage || value === undefined || value === null) {
                        return;
                    }

                    try {
                        storage.setItem(key, typeof value === 'string' ? value : JSON.stringify(value));
                    } catch {
                    }
                };

                for (const storage of [window.localStorage, window.sessionStorage]) {
                    for (const [key, value] of Object.entries(storageItems)) {
                        writeValue(storage, key, value);
                    }
                }

                window.__HOST_AUTH__ = { ...(window.__HOST_AUTH__ || {}), ...hostAuth };
                window.__KURO_HOST_ENV__ = hostEnv;
            })();
            """;
        }

        private static Dictionary<string, object?> CreateHostAuth(WebSessionContext session)
        {
            return new(StringComparer.Ordinal)
            {
                ["token"] = session.Token,
                ["did"] = session.Did,
                ["userId"] = session.UserId,
                ["serverId"] = session.ServerId,
                ["roleId"] = session.RoleId,
                ["serverName"] = session.ServerName,
                ["roleName"] = session.RoleName,
                ["requestIp"] = session.RequestIp,
                ["userName"] = session.UserName,
                ["headUrl"] = session.HeadUrl,
                ["appVersion"] = session.AppVersion,
                ["channelId"] = session.ChannelId,
                ["enterSource"] = session.EnterSource,
                ["ua"] = session.UserAgentName,
                ["os"] = session.Os,
                ["gameId"] = session.GameId
            };
        }

        private static void ApplyCookieSession(WebView2 webView, WebSessionContext session)
        {
            if (webView.CoreWebView2 is null)
            {
                return;
            }

            var cookieManager = webView.CoreWebView2.CookieManager;
            SetCookie(cookieManager, KuroWebStaticOrigin, "token", session.Token);
            SetCookie(cookieManager, KuroWebStaticOrigin, "did", session.Did);
            SetCookie(cookieManager, KuroWebStaticOrigin, "userId", session.UserId);
            SetCookie(cookieManager, KuroBbsOrigin, "token", session.Token);
            SetCookie(cookieManager, KuroBbsOrigin, "did", session.Did);
            SetCookie(cookieManager, KuroBbsOrigin, "userId", session.UserId);
        }

        private static void SetCookie(CoreWebView2CookieManager cookieManager, Uri origin, string name, string value)
        {
            var cookie = cookieManager.CreateCookie(name, value ?? string.Empty, origin.Host, "/");
            cookie.IsHttpOnly = false;
            cookie.IsSecure = true;
            cookie.SameSite = CoreWebView2CookieSameSiteKind.None;
            cookie.Expires = DateTime.Now.AddDays(7).Ticks;
            cookieManager.AddOrUpdateCookie(cookie);
        }
    }

}
