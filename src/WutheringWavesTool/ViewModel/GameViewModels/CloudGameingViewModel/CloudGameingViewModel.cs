using Haiyu.Common.KuroWebView;
using System;
using System.Collections.Generic;
using System.Text;
using Waves.Api.Models.CloudGame;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.Models.CloudGame;
using Waves.Core.Services;

namespace Haiyu.ViewModel.GameViewModels;

public sealed partial class CloudGameingViewModel:ViewModelBase
{
    
    public WebView2 WebView2 { get; set; }
    public Window Window { get; set; }
    public BrowserSessionLaunchOptions Option { get; set; }
    public nint WindowHandle { get; private set; }
    public IKuroCloudGameContext KuroCloudGameContext { get; }
    public CloudGameingViewModel([FromKeyedServices(nameof(KuroCloudGameContext))] IKuroCloudGameContext kuroCloudGameContext)
    {
        this.KuroCloudGameContext = kuroCloudGameContext;
        this.KuroCloudGameContext.CloudGameProcessTracker.OnProgressChanged += CloudGameProcessTracker_OnProgressChanged;
        RegisterMessanger();
    }

    private void RegisterMessanger()
    {
        this.Messenger.Register<CloudQualityUpdateModel>(this,QualityUpdateChanged);
    }

    private async void QualityUpdateChanged(object recipient, CloudQualityUpdateModel message)
    {
        if (WebView2.CoreWebView2 == null)
            return;
        var dpi = (int)HwndExtensions.GetDpiForWindow(Window.GetWindowHandle());
        var area = DisplayArea.Primary.OuterBounds;
        var option =  await KuroCloudGameContext.GetOptionsAsync(dpi, area.Width, area.Height);
        var script = CloudGameBuilder.BuildUpdateQalityScript(option);
        await WebView2.CoreWebView2.ExecuteScriptAsync(script);
        await this.UpdateNetworkVisiblity();
    }



    private void CloudGameProcessTracker_OnProgressChanged(Waves.Core.Services.CloudGameServices.CloudGameProcessTracker obj)
    {
        //终止游戏
        if(obj.CoreType == Waves.Core.Models.Enums.CloudCoreType.ReqExit)
        {
            this.Window.DispatcherQueue.TryEnqueue(() =>
            {
                this.WebView2.Close();
                Window.Close();
            });
            this.KuroCloudGameContext.CloudGameEventPublisher.Publish(new( Waves.Core.Models.Enums.CloudCoreType.None));
        }
    }

    public void SetWebView(WebView2 webView2, Window window, BrowserSessionLaunchOptions option)
    {
        ArgumentNullException.ThrowIfNull(webView2);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(option);
        this.WebView2 = webView2;
        this.Window = window;
        this.Option = option;
        this.Window.Closed += Window_Closed;
        this.Window.Activated += Window_Activated;
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        ApplyWindowActivationState(
            args.WindowActivationState != WindowActivationState.Deactivated
        );
    }

    private async void Window_Closed(object sender, WindowEventArgs args)
    {
        await RequestExitAsync();
        this.KuroCloudGameContext.ClearWindow();
        this.KuroCloudGameContext.CloudGameEventPublisher.Publish(new(Waves.Core.Models.Enums.CloudCoreType.None));
        this.KuroCloudGameContext.CloudGameProcessTracker.OnProgressChanged -= CloudGameProcessTracker_OnProgressChanged;
        this.ShowSystemCursor();
    }

    [RelayCommand]
    async Task Loaded()
    {
        WebView2!.NavigationStarting += Browser_NavigationStarting;
        WebView2.NavigationCompleted += Browser_NavigationCompleted;
        this.WindowHandle = Window.GetWindowHandle();
        await WebView2.EnsureCoreWebView2Async();
        WebView2.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        WebView2.CoreWebView2.Settings.AreDevToolsEnabled = true;
        WebView2.CoreWebView2.Settings.IsPinchZoomEnabled = false;
        WebView2.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;
        WebView2.CoreWebView2.Settings.IsStatusBarEnabled = false;
        WebView2.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

        StartHotkeyTimer();

        await ApplyLaunchOptionsAsync();

        WebView2.CoreWebView2.Navigate("https://kuro-stream.local/bridge.html");

        #region NetworkVisiblity
        await UpdateNetworkVisiblity();
        #endregion
    }

    async Task UpdateNetworkVisiblity()
    {
        var networkOpen = await this.KuroCloudGameContext.GameLocalConfig.GetConfigAsync(CloudGameLocalSettingName.EnableNetworkPanel);
        if (bool.TryParse(networkOpen, out var enableNetworkPanel))
        {
            this.NetworkVisibility = enableNetworkPanel ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private async Task ApplyLaunchOptionsAsync()
    {
        if (WebView2?.CoreWebView2 is null)
        {
            return;
        }

        var core = WebView2.CoreWebView2;

        var requiresWebResourceInterceptor = false;

        if (Option.StreamOptions is not null)
        {
            core.AddWebResourceRequestedFilter(
                "https://kuro-stream.local/bridge.html",
                CoreWebView2WebResourceContext.Document
            );
            requiresWebResourceInterceptor = true;
        }

        if (Option.AdditionalHeaders.Count > 0)
        {
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            requiresWebResourceInterceptor = true;
        }

        if (requiresWebResourceInterceptor)
        {
            core.WebResourceRequested += CoreWebView2_WebResourceRequested;
        }

        if (Option.StreamOptions is not null)
        {
            return;
        }

        ApplyCookies(core.CookieManager);

        var bootstrapScript = BuildBootstrapScript();
        if (!string.IsNullOrWhiteSpace(bootstrapScript))
        {
            await core.AddScriptToExecuteOnDocumentCreatedAsync(bootstrapScript);
        }
    }
    private string BuildBootstrapScript()
    {
        if (
            string.IsNullOrWhiteSpace(Option.AccessToken)
            && string.IsNullOrWhiteSpace(Option.RefreshToken)
            && Option.StorageItems.Count == 0
        )
        {
            return string.Empty;
        }

        var payloadJson = JsonSerializer.Serialize(
            new CloudBootstrapScript()
            {
                AccessToken = Option.AccessToken,
                RefreshToken = Option.RefreshToken,
                StorageItems = Option.StorageItems,
            },
            CloudGameContext.Default.CloudBootstrapScript
        );
        return CloudGameBuilder.BuildBootstrapScript(payloadJson);
    }

    private void ApplyCookies(CoreWebView2CookieManager cookieManager)
    {
        if (Option.Cookies.Count == 0)
        {
            return;
        }

        var domain = Option.CookieDomain;
        var isSecure =
            Uri.TryCreate(
                "https://mc.kurogames.com/cloud/index.html",
                UriKind.Absolute,
                out var uri
            )
            && string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase
            );

        foreach (var pair in Option.Cookies)
        {
            var cookie = cookieManager.CreateCookie(pair.Key, pair.Value, domain, "/");
            cookie.IsSecure = isSecure;
            cookie.IsHttpOnly = false;
            cookie.SameSite = CoreWebView2CookieSameSiteKind.None;
            cookieManager.AddOrUpdateCookie(cookie);
        }
    }

    private async void CoreWebView2_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        if (
            WebView2?.CoreWebView2 is not null
            && Option.StreamOptions is not null
            && string.Equals(
                args.Request.Uri,
                "https://kuro-stream.local/bridge.html",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            InMemoryRandomAccessStream randomAccessStream = new InMemoryRandomAccessStream();
            Stream outputStream = randomAccessStream.AsStreamForRead();
            randomAccessStream.Seek(0);
            string html = BuildStreamBridgeHtml(Option);

            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(html));
            args.Response = WebView2.CoreWebView2.Environment.CreateWebResourceResponse(
                await stream.ConvertStreamToRandomAccessStream(),
                200,
                "OK",
                "Content-Type: text/html; charset=utf-8"
            );
            return;
        }

        if (!Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out var requestUri))
        {
            return;
        }

        foreach (var pair in Option.AdditionalHeaders)
        {
            try
            {
                args.Request.Headers.SetHeader(pair.Key, pair.Value);
            }
            catch { }
        }
    }


    private string BuildStreamBridgeHtml(BrowserSessionLaunchOptions option)
    {
        var dispatchMessageJson = option.StreamOptions.DispatchMessage;
        var storageItemsJson = JsonSerializer.Serialize(
            option.StorageItems,
            CloudGameContext.Default.IReadOnlyDictionaryStringString
        );

        var scriptUrlJson = $"\"{option.StreamOptions.ScriptUrl}\"";
        var bridgeConfigJson = JsonSerializer.Serialize(
            new BridgeConfig()
            {
                Id = "kuro-stream-surface",
                TenantKey = Option.StreamOptions.TenantKey,
                IspUrl = "https://paas-sdk.vlinkcloud.cn",
                VideoPoster = string.Empty,
                GameId = Option.StreamOptions.GameId,
                NodeId = string.Empty,
                EnableClipBoard = true,
                MouseShortcut = (string?)null,
                LockPoint = true,
                EnvType = "pc",
                FillVideo = false,
                EnableInitSpeed = false,
                UseGamePlayLayer = true,
                EnableReportLog = true,
                EnableReconnect = true,
                BitRate = Option.Quality.BitRate,
                BitRateMin = Option.Quality.BitRateMin,
                BitRateMax = Option.Quality.BitRateMax,
                Fps = Option.Quality.Fps,
                TargetWidth = Option.Quality.Width,
                TargetHeight = Option.Quality.Height,
                CodecType = Option.Quality.CodecType,
                StreamStrategy = Option.Quality.StreamStrategy,
                EnableImageEnhancement = Option.Quality.EnableImageEnhancement,
                Dpi = Option.StreamDpi,
            }, CloudGameContext.Default.BridgeConfig
        );

        return CloudGameBuilder.BuildDefaultBridgeHtml(scriptUrlJson, dispatchMessageJson, bridgeConfigJson, storageItemsJson);
    }



    private void CoreWebView2_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var model = JsonSerializer.Deserialize(args.WebMessageAsJson, CloudGameContext.Default.WelinkMessage);

            switch (model.Type)
            {
                case "status":
                    break;
                case "warning":
                    break;
                case "launch-dispatched":
                    break;
                case "first-frame":
                    break;
                case "quality-change":
                    break;
                case "network-stat":
                    UpdateNetworkDisplay(model);
                    break;
                case "cursor-data":
                    ApplyCloudCursorVisibility(model.Visible);
                    break;
                case "error":
                    ShowSystemCursor();
                    break;
            }
        }
        catch (Exception ex) { }
    }

    private void Browser_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        TryInstallWebViewCursorSubclass();

        if (!args.IsSuccess)
        {
            ShowSystemCursor();
        }
    }

    private void Browser_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        ShowSystemCursor();
    }
}
