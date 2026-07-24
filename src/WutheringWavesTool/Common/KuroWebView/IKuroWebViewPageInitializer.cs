using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Common.KuroWebView;


public interface IKuroWebViewPageInitializer
{
    bool CanInitialize(WebSessionContext session);

    Task InitializeAsync(WebView2 webView, WebSessionContext session);

    Task ApplySessionAsync(WebView2 webView, WebSessionContext session);
}
