using ABI.System;
using Haiyu.Common.KuroWebView;

namespace Haiyu.WindowModels;

public sealed partial class KuroDataCenterWindow : WindowModelBase
{
    WebViewHostInitializer hostInitializer;

    public KuroDataCenterWindow(nint value, WebSessionContext context)
        : base(value)
    {
        InitializeComponent();
        this.titleBar.Window = this;
        this.AppWindow.Closing += AppWindow_Closing;
        Context = context;
    }

    public WebSessionContext Context { get; }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (webView2 != null)
        {
            webView2.Close();
        }
        this.AppWindow.Closing -= AppWindow_Closing;
    }

    private async void grid_Loaded(object sender, RoutedEventArgs e)
    {
        hostInitializer = new WebViewHostInitializer();
        await hostInitializer.InitializeAsync(webView2, Context);
        this.webView2.CoreWebView2.Navigate(Context.GetDataCenterUrl());
    }
}
