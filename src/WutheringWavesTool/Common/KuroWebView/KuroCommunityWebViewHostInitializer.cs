using System.Text.Encodings.Web;
using System.Text.Json;
using Haiyu.Common.KuroWebView.Initializer;

namespace Haiyu.Common.KuroWebView;


public sealed class KuroCommunityWebViewHostInitializer
{
    private readonly KuroWebViewInitializerResolver _resolver = new();
    private IKuroWebViewPageInitializer? _currentInitializer;

    public async Task InitializeAsync(WebView2 webView, WebSessionContext session)
    {
        _currentInitializer = _resolver.Resolve(session);
        await _currentInitializer.InitializeAsync(webView, session);
    }

    public async Task ApplySessionAsync(WebView2 webView, WebSessionContext session)
    {
        _currentInitializer ??= _resolver.Resolve(session);
        await _currentInitializer.ApplySessionAsync(webView, session);
    }
}

public sealed class KuroWebViewInitializerResolver
{
    private readonly IKuroWebViewPageInitializer[] _initializers =
    [
        new KuroDataPanelWebViewInitializer(),
        new KuroGrowthCalculatorWebViewInitializer(),
        new KuroCalendarWebViewInitializer(),
        new KuroMapWebViewInitializer(),
        new KuroResourceBriefingWebViewInitializer(),
        new KuroFallbackCommunityWebViewInitializer()
    ];

    public IKuroWebViewPageInitializer Resolve(WebSessionContext session)
    {
        return _initializers.First(initializer => initializer.CanInitialize(session));
    }
}






/// <summary>
/// 兜底库街区 WebView。
/// URL: 未匹配到专用规则的 web-static.kurobbs.com / www.kurobbs.com 页面。
/// Query: 保留原 URL，不额外假设页面参数。
/// 初始化规则: 通用用户信息、角色信息和完整 bridge 兜底。
/// </summary>
public sealed class KuroFallbackCommunityWebViewInitializer : KuroWebViewPageInitializerBase
{
    public override bool CanInitialize(WebSessionContext session)
    {
        return true;
    }

    protected override Dictionary<string, object?> CreateStorageItems(WebSessionContext session)
    {
        return new(StringComparer.Ordinal)
        {
            ["token"] = session.Token,
            ["did"] = session.Did,
            ["userId"] = session.UserId,
            ["initUserInfo"] = CreateUserInfo(session),
            ["mc_userInfo"] = CreateCompactRoleInfo(session),
            ["REQUEST_IP"] = session.RequestIp
        };
    }
}
