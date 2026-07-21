using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Common.KuroWebView.Initializer
{
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
}
