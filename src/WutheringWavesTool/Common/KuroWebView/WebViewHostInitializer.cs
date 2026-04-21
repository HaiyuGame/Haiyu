using System.Text.Encodings.Web;

namespace Haiyu.Common.KuroWebView;

public sealed class WebViewHostInitializer
{
    private const string AndroidAppUserAgent = "Mozilla/5.0 (Linux; Android 13; 23049RAD8C Build/TQ3A.230805.001; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/124.0.0.0 Mobile Safari/537.36 KuroGameBox/2.2.2 KR Android";
    private static readonly Uri KuroMcBoxOrigin = new("https://web-static.kurobbs.com/");

    private string? _documentScriptId;
    private WebSessionContext _currentSession;
    private bool _eventsHooked;

    public async Task InitializeAsync(WebView2 webView, WebSessionContext session)
    {
        _currentSession = session;

        var environment = await CoreWebView2Environment.CreateAsync();
        await webView.EnsureCoreWebView2Async(environment);
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
            throw new InvalidOperationException("WebView2 尚未初始化。");
        }

        ApplyCookieSession(webView, session);
        await webView.CoreWebView2.ExecuteScriptAsync(BuildApplySessionScript(session));
    }

    private async Task InstallSessionScriptAsync(WebView2 webView, WebSessionContext session)
    {
        if (webView.CoreWebView2 is null)
        {
            throw new InvalidOperationException("WebView2 尚未初始化。");
        }

        if (!string.IsNullOrWhiteSpace(_documentScriptId))
        {
            return;
        }

        _documentScriptId = await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BuildBootstrapScript(session));
    }

    private void HookEvents(WebView2 webView)
    {
        if (_eventsHooked || webView.CoreWebView2 is null)
        {
            return;
        }

        webView.CoreWebView2.NavigationCompleted += async (_, _) =>
        {
            await webView.CoreWebView2.ExecuteScriptAsync(BuildApplySessionScript(_currentSession));
        };

        _eventsHooked = true;
    }

    private static string BuildBootstrapScript(WebSessionContext session)
    {
        var json = JsonSerializer.Serialize(new KuroSession
        {
            Token = session.Token,
            Did = session.Did,
            UserId = session.UserId
        },KuroSessionContext.Default.KuroSession);

        return $$"""
            (() => {
                const bootstrapSession = {{json}};
                const asJson = (value) => JSON.stringify(value ?? {});
                const ok = (payload) => asJson(payload);

                function getHostAuth() {
                    const fallback = {
                        token: bootstrapSession.token ?? '',
                        did: bootstrapSession.did ?? '',
                        userId: bootstrapSession.userId ?? ''
                    };

                    if (window.__HOST_AUTH__) {
                        return {
                            token: window.__HOST_AUTH__.token ?? fallback.token,
                            did: window.__HOST_AUTH__.did ?? fallback.did,
                            userId: window.__HOST_AUTH__.userId ?? fallback.userId
                        };
                    }

                    try {
                        return {
                            token: window.localStorage.getItem('token') ?? fallback.token,
                            did: window.localStorage.getItem('did') ?? fallback.did,
                            userId: window.localStorage.getItem('userId') ?? fallback.userId
                        };
                    } catch {
                        return fallback;
                    }
                }

                function applySession() {
                    const values = getHostAuth();

                    const initUserInfo = {
                        userId: values.userId,
                        token: values.token,
                        did: values.did,
                        channelId: 1
                    };

                    for (const [key, value] of Object.entries(values)) {
                        try {
                            window.localStorage.setItem(key, value);
                            window.sessionStorage.setItem(key, value);
                        } catch {
                        }
                    }

                    try {
                        window.localStorage.setItem('initUserInfo', JSON.stringify(initUserInfo));
                        window.sessionStorage.setItem('initUserInfo', JSON.stringify(initUserInfo));
                    } catch {
                    }

                    window.__HOST_AUTH__ = values;
                    window.__KURO_HOST_ENV__ = {
                        platform: 'android',
                        appName: 'KuroGameBox'
                    };
                }

                function createResponse(handlerName, data) {
                    const values = getHostAuth();

                    switch (handlerName) {
                        case 'getUserInfo':
                            return ok({ token: values.token, did: values.did, userId: values.userId, channelId: 1 });
                        case 'refreshToken':
                        case 'refreshTokenV2':
                            return ok({ code: 0, token: values.token, userId: values.userId, did: values.did, channelId: 1 });
                        case 'getSystemStatus':
                            return ok({ darkMode: false, theme: 'light' });
                        case 'setToolInfo':
                        case 'toSkip':
                        case 'appShare':
                        case 'finishPage':
                        case 'useSystemSetting':
                            return ok({ result: true });
                        default:
                            return ok({ result: true, echo: data ?? null, handlerName });
                    }
                }

                const bridge = {
                    init() {
                    },
                    callHandler(handlerName, data, callback) {
                        applySession();
                        const response = createResponse(handlerName, data);
                        if (typeof callback === 'function') {
                            setTimeout(() => callback(response), 0);
                        }
                    },
                    registerHandler(handlerName, callback) {
                        this._handlers = this._handlers || {};
                        this._handlers[handlerName] = callback;
                    }
                };

                window.WebViewJavascriptBridge = bridge;
                window.WVJBCallbacks = window.WVJBCallbacks || [];
                applySession();

                setTimeout(() => {
                    document.dispatchEvent(new Event('WebViewJavascriptBridgeReady'));
                }, 0);
            })();
            """;
    }

    private static string BuildApplySessionScript(WebSessionContext session)
    {
        var encodedToken = JavaScriptEncoder.Default.Encode(session.Token ?? string.Empty);
        var encodedDid = JavaScriptEncoder.Default.Encode(session.Did ?? string.Empty);
        var encodedUserId = JavaScriptEncoder.Default.Encode(session.UserId ?? string.Empty);

        return $$"""
            (() => {
                const values = {
                    token: '{{encodedToken}}',
                    did: '{{encodedDid}}',
                    userId: '{{encodedUserId}}'
                };

                const initUserInfo = {
                    userId: values.userId,
                    token: values.token,
                    did: values.did,
                    channelId: 1
                };

                for (const [key, value] of Object.entries(values)) {
                    try {
                        window.localStorage.setItem(key, value);
                        window.sessionStorage.setItem(key, value);
                    } catch {
                    }
                }

                try {
                    window.localStorage.setItem('initUserInfo', JSON.stringify(initUserInfo));
                    window.sessionStorage.setItem('initUserInfo', JSON.stringify(initUserInfo));
                } catch {
                }

                window.__HOST_AUTH__ = values;
                window.__KURO_HOST_ENV__ = {
                    platform: 'android',
                    appName: 'KuroGameBox'
                };
            })();
            """;
    }

    private static void ApplyCookieSession(WebView2 webView, WebSessionContext session)
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        var cookieManager = webView.CoreWebView2.CookieManager;
        SetCookie(cookieManager, "token", session.Token);
        SetCookie(cookieManager, "did", session.Did);
        SetCookie(cookieManager, "userId", session.UserId);
    }

    private static void SetCookie(CoreWebView2CookieManager cookieManager, string name, string value)
    {
        var cookie = cookieManager.CreateCookie(name, value ?? string.Empty, KuroMcBoxOrigin.Host, "/");
        cookie.IsHttpOnly = false;
        cookie.IsSecure = true;
        cookie.SameSite = CoreWebView2CookieSameSiteKind.None;
        cookie.Expires = DateTime.Now.AddDays(7).Ticks;
        cookieManager.AddOrUpdateCookie(cookie);
    }
}