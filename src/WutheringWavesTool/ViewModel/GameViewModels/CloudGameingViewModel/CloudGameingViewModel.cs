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
    private const string StreamBridgeUrl = "https://mc.kurogames.com/cloud/haiyu-stream-bridge.html";
    
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
        Logger.WriteInfo($"[CloudGame] SetWebView provider={option.StreamOptions?.ProviderType}, dpi={option.StreamDpi}, quality={option.Quality?.Width}x{option.Quality?.Height}");
        this.Window.Closed += Window_Closed;
        this.Window.Activated += Window_Activated;
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        var active = args.WindowActivationState != WindowActivationState.Deactivated;
        Logger.WriteInfo($"[CloudGame][Active] WindowActivated state={args.WindowActivationState}, active={active}");
        ApplyWindowActivationState(active);
    }

    private async void Window_Closed(object sender, WindowEventArgs args)
    {
        StopCloudSessionKeepAlive();
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
        await WebView2EnvironmentProvider.EnsureInitializedAsync(WebView2);
        WebView2.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        WebView2.CoreWebView2.Settings.AreDevToolsEnabled = false;
        WebView2.CoreWebView2.Settings.IsPinchZoomEnabled = false;
        WebView2.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;
        WebView2.CoreWebView2.Settings.IsStatusBarEnabled = false;
        WebView2.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        WebView2.CoreWebView2.ProcessFailed += CoreWebView2_ProcessFailed;
        StartHotkeyTimer();

        if (WebView2EnvironmentProvider.CdpPort > 0)
        {
            Logger.WriteInfo(
                $"[CloudGame][CDP] WebView2 remote-debugging-port={WebView2EnvironmentProvider.CdpPort} " +
                $"(http://127.0.0.1:{WebView2EnvironmentProvider.CdpPort}/json/list)"
            );
        }

        await ApplyLaunchOptionsAsync();

        WebView2.CoreWebView2.Navigate(StreamBridgeUrl);
        StartCloudSessionKeepAlive();

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
                StreamBridgeUrl,
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
                StreamBridgeUrl,
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
        if (option.StreamOptions.ProviderType == 3)
        {
            var tencentQualityJson = JsonSerializer.Serialize(
                new BridgeConfig()
                {
                    BitRate = option.Quality.BitRate,
                    BitRateMin = option.Quality.BitRateMin,
                    BitRateMax = option.Quality.BitRateMax,
                    Fps = option.Quality.Fps,
                    TargetWidth = option.Quality.Width,
                    TargetHeight = option.Quality.Height,
                    EnableImageEnhancement = option.Quality.EnableImageEnhancement,
                },
                CloudGameContext.Default.BridgeConfig
            );
            return CloudGameBuilder.BuildTencentBridgeHtml(
                JsonSerializer.Serialize(option.StreamOptions.TencentUserKey, CloudGameContext.Default.String),
                JsonSerializer.Serialize(option.StreamOptions.TencentDeviceId, CloudGameContext.Default.String),
                JsonSerializer.Serialize(option.StreamOptions.TencentAllocRespJson, CloudGameContext.Default.String),
                JsonSerializer.Serialize(option.StreamOptions.TencentToken, CloudGameContext.Default.String),
                tencentQualityJson
            );
        }

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
                // WebView2 needs the gameplay input layer; without it keyboard/mouse
                // events never reach the remote stream after pointer lock / resize.
                EnvType = "pc",
                FillVideo = false,
                EnableInitSpeed = true,
                UseGamePlayLayer = false,
                EnableReplenishEsc = true,
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
                // Super-resolution / enhance path has crashed WebView2 browser process
                // (D3D11VideoDecoder + msedge.dll 0x80000003). Keep it off in host bridge.
                EnableImageEnhancement = false,
                Dpi = Option.StreamDpi,
            }, CloudGameContext.Default.BridgeConfig
        );

        return CloudGameBuilder.BuildDefaultBridgeHtml(scriptUrlJson, dispatchMessageJson, bridgeConfigJson, storageItemsJson);
    }



    private void CoreWebView2_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        // Bridge posts many shapes of "detail" (string/number/object). Never bind the
        // whole payload to NetworkDetail or pivotal frames throw and flood the log.
        try
        {
            using var doc = JsonDocument.Parse(args.WebMessageAsJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl))
            {
                return;
            }

            var type = typeEl.GetString() ?? string.Empty;

            switch (type)
            {
                case "network-stat":
                {
                    var model = JsonSerializer.Deserialize(
                        args.WebMessageAsJson,
                        CloudGameContext.Default.WelinkMessage
                    );
                    if (model?.Detail is not null)
                    {
                        UpdateNetworkDisplay(model);
                    }
                    break;
                }
                case "cursor-data":
                {
                    var visible = root.TryGetProperty("visible", out var v) && v.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.Number => v.TryGetInt32(out var number) && number != 0,
                        JsonValueKind.String => bool.TryParse(v.GetString(), out var parsed) && parsed,
                        _ => false
                    };
                    ApplyCloudCursorVisibility(visible);
                    break;
                }
                case "first-frame":
                    Logger.WriteInfo("[CloudGame][Bridge] first-frame received");
                    break;
                case "game-resolution":
                case "resolution-sync":
                {
                    var reason = root.TryGetProperty("reason", out var rr) ? rr.ToString() : "";
                    var w = root.TryGetProperty("width", out var we) ? we.ToString() : "?";
                    var h = root.TryGetProperty("height", out var he) ? he.ToString() : "?";
                    var action = root.TryGetProperty("action", out var ae) ? ae.ToString() : "";
                    var seq = root.TryGetProperty("seq", out var se) ? se.ToString() : "";
                    Logger.WriteInfo(
                        $"[CloudGame][SizeProbe] bridge {type} reason={reason} seq={seq} " +
                        $"{w}x{h} action={action}"
                    );
                    break;
                }
                case "diagnostic":
                {
                    var reason = root.TryGetProperty("reason", out var r) ? r.ToString() : "";
                    Logger.WriteInfo($"[CloudGame][Diag] {reason}");
                    break;
                }
                case "warning":
                    Logger.WriteWarning($"[CloudGame][Bridge][warning] {args.WebMessageAsJson}");
                    break;
                case "error":
                    Logger.WriteError($"[CloudGame][Bridge][error] {args.WebMessageAsJson}");
                    ShowSystemCursor();
                    break;
                case "pivotal":
                case "status":
                case "keepalive":
                case "game-send":
                case "sdk-message":
                case "launch-dispatched":
                case "quality-change":
                case "image-enhancement":
                    // High-frequency / low-value; keep out of default log.
                    break;
                default:
                    Logger.WriteInfo($"[CloudGame][Bridge] {type}: {TruncateForLog(args.WebMessageAsJson, 400)}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.WriteError(
                $"[CloudGame][Bridge] message handle failed: {ex.GetType().Name}: {ex.Message}; payload={TruncateForLog(args.WebMessageAsJson, 300)}"
            );
        }
    }

    private static string TruncateForLog(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, max) + $"...(+{value.Length - max})";
    }

    private void Browser_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        Logger.WriteInfo($"[CloudGame] NavigationCompleted success={args.IsSuccess}, status={args.HttpStatusCode}, error={args.WebErrorStatus}");

        if (!args.IsSuccess)
        {
            ShowSystemCursor();
        }
    }

    private void Browser_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        Logger.WriteInfo($"[CloudGame] NavigationStarting uri={args.Uri}");
        ShowSystemCursor();
    }

    private void CoreWebView2_ProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args)
    {
        // BrowserProcessExited = entire Chromium process died; CoreWebView2 becomes null.
        // Collect every field WebView2 exposes — this is the best in-app signal we get.
        try
        {
            var exitHex = $"0x{(uint)args.ExitCode:X8}";
            var module = string.Empty;
            var description = string.Empty;
            var frames = string.Empty;
            try { module = args.FailureSourceModulePath ?? string.Empty; } catch { /* older runtime */ }
            try { description = args.ProcessDescription ?? string.Empty; } catch { }
            try
            {
                if (args.FrameInfosForFailedProcess is { Count: > 0 } list)
                {
                    frames = string.Join(
                        " | ",
                        list.Select(f =>
                        {
                            try
                            {
                                return $"name={f.Name};src={f.Source}";
                            }
                            catch
                            {
                                return f?.ToString() ?? "?";
                            }
                        })
                    );
                }
            }
            catch { }

            var appWindow = Window?.AppWindow;
            Logger.WriteError(
                $"[CloudGame][ProcessFailed] kind={args.ProcessFailedKind}, reason={args.Reason}, " +
                $"exitCode={args.ExitCode} ({exitHex}), module={module}, desc={description}, frames=[{frames}], " +
                $"winSize={appWindow?.Size.Width}x{appWindow?.Size.Height}, " +
                $"wv={WebView2?.ActualWidth:0.#}x{WebView2?.ActualHeight:0.#}, " +
                $"cdpPort={WebView2EnvironmentProvider.CdpPort}, " +
                $"time={DateTime.Now:O}"
            );

            // Persist a dedicated crash breadcrumb next to app logs for easy sharing.
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Waves",
                    "appLogs"
                );
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"webview-crash-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                File.WriteAllText(
                    path,
                    string.Join(
                        Environment.NewLine,
                        [
                            $"time={DateTime.Now:O}",
                            $"kind={args.ProcessFailedKind}",
                            $"reason={args.Reason}",
                            $"exitCode={args.ExitCode}",
                            $"exitHex={exitHex}",
                            $"module={module}",
                            $"description={description}",
                            $"frames={frames}",
                            $"win={appWindow?.Position.X},{appWindow?.Position.Y} {appWindow?.Size.Width}x{appWindow?.Size.Height}",
                            $"presenter={appWindow?.Presenter.Kind}",
                            $"wvActual={WebView2?.ActualWidth}x{WebView2?.ActualHeight}",
                            $"dpi={Option?.StreamDpi}",
                            $"quality={Option?.Quality?.Width}x{Option?.Quality?.Height}",
                            $"codec={Option?.Quality?.CodecType}",
                            $"cdpPort={WebView2EnvironmentProvider.CdpPort}",
                            $"userData={AppSettings.WebCacheFolder}",
                            "",
                            "Note: BrowserProcessExited means the WebView2 Chromium process exited.",
                            "Check Windows Event Viewer > Application for WebView2/Edge crash entries,",
                            "and %LOCALAPPDATA%\\CrashDumps for *.dmp next to this timestamp.",
                        ]
                    )
                );
                Logger.WriteError($"[CloudGame][ProcessFailed] crash report written: {path}");
            }
            catch (Exception writeEx)
            {
                Logger.WriteError($"[CloudGame][ProcessFailed] crash report write failed: {writeEx.Message}");
            }

            ShowSystemCursor();

            // Render-process-only failures can often recover with a reload.
            // Browser process death requires a new WebView control (session is gone).
            if (args.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited)
            {
                Window?.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        Logger.WriteWarning("[CloudGame][ProcessFailed] attempting Reload after render process exit");
                        WebView2?.CoreWebView2?.Reload();
                    }
                    catch (Exception reloadEx)
                    {
                        Logger.WriteError($"[CloudGame][ProcessFailed] Reload failed: {reloadEx}");
                    }
                });
            }
            else if (args.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
            {
                Logger.WriteError(
                    "[CloudGame][ProcessFailed] Browser process dead — CoreWebView2 is unusable. " +
                    "Close the cloud window and re-enter. See Documents\\Waves\\appLogs\\webview-crash-*.txt " +
                    "and Documents\\Waves\\appLogs\\webview2-chromium.log / %LOCALAPPDATA%\\CrashDumps"
                );
            }
        }
        catch (Exception ex)
        {
            Logger.WriteError($"[CloudGame][ProcessFailed] log failed: {ex}");
        }
    }
}
