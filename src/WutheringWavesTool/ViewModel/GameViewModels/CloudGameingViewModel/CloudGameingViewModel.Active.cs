using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Waves.Api.Models.CloudGame;

namespace Haiyu.ViewModel.GameViewModels;

partial class CloudGameingViewModel
{
    [ObservableProperty]
    public partial string DelayTime { get; set; }

    [ObservableProperty]
    public partial string Fps { get; set; }

    [ObservableProperty]
    public partial string Network { get; set; }

    [ObservableProperty]
    public partial string PacketLossRate { get; set; }

    [ObservableProperty]
    public partial Visibility TitleBarVisiblity { get; set; }


    [ObservableProperty]
    public partial Visibility NetworkVisibility { get; set; }
    [ObservableProperty]
    public partial double VolumeValue { get; set; } = 100;

    public void UpdateNetworkDisplay(WelinkMessage message)
    {
        this.Window.DispatcherQueue.TryEnqueue(() =>
        {
            this.DelayTime = LanguageService.FormatByText(LanguageService.GetStringByText("延迟：{0} ms"), message.Detail.NetWorkDelay);
            this.Fps = LanguageService.FormatByText(LanguageService.GetStringByText("客户端：{0}帧"), message.Detail.Fps);
            this.Network = LanguageService.FormatByText(LanguageService.GetStringByText("带宽：{0:0.#} MB/s"), message.Detail.Bitrate / 8 / 1024.0);
            this.PacketLossRate = LanguageService.FormatByText(LanguageService.GetStringByText("丢包率：{0}%"), message.Detail.PacketLossRate);
        });
    }

    private int _sizeChangedSeq;

    /// <summary>
    /// Probe mode: SizeChanged does NOTHING except log.
    /// If maximize/resize still causes BrowserProcessExited with only these lines
    /// and no ExecuteScript / setGameResolution, the crash is NOT from our SizeChanged logic.
    /// </summary>
    [RelayCommand]
    Task SizeChanged()
    {
        var seq = Interlocked.Increment(ref _sizeChangedSeq);
        try
        {
            var app = Window?.AppWindow;
            var wv = WebView2;
            Logger.WriteInfo(
                $"[CloudGame][SizeProbe] #{seq} ENTER " +
                $"win={app?.Size.Width}x{app?.Size.Height} " +
                $"pos={app?.Position.X},{app?.Position.Y} " +
                $"wv={wv?.ActualWidth:0.#}x{wv?.ActualHeight:0.#} " +
                $"core={(wv?.CoreWebView2 is null ? "null" : "ok")} " +
                $"action=NONE"
            );
        }
        catch (Exception ex)
        {
            Logger.WriteWarning($"[CloudGame][SizeProbe] #{seq} log failed: {ex.Message}");
        }

        // Intentionally no: debounce, ExecuteScript, layoutSurface, setGameResolution,
        // SyncBridgeResolutionAsync, dispatchEvent('resize').
        Logger.WriteInfo($"[CloudGame][SizeProbe] #{seq} EXIT action=NONE");
        return Task.CompletedTask;
    }

    private void LogHostWindowMetrics(string stage)
    {
        try
        {
            var appWindow = Window?.AppWindow;
            var wv = WebView2;
            Logger.WriteInfo(
                $"[CloudGame][Size] {stage}: " +
                $"winPos={appWindow?.Position.X},{appWindow?.Position.Y} " +
                $"winSize={appWindow?.Size.Width}x{appWindow?.Size.Height} " +
                $"presenter={appWindow?.Presenter.Kind} " +
                $"borderlessFs={_isBorderlessFullScreen} " +
                $"wvActual={wv?.ActualWidth:0.#}x{wv?.ActualHeight:0.#} " +
                $"wvVisible={wv?.Visibility} " +
                $"coreWv={(wv?.CoreWebView2 is null ? "null" : "ok")} " +
                $"dpi={Option?.StreamDpi} " +
                $"quality={Option?.Quality?.Width}x{Option?.Quality?.Height} " +
                $"bitRate={Option?.Quality?.BitRate} fps={Option?.Quality?.Fps} codec={Option?.Quality?.CodecType}"
            );
        }
        catch (Exception ex)
        {
            Logger.WriteError($"[CloudGame][Size] metrics {stage} failed: {ex}");
        }
    }

    async partial void OnVolumeValueChanged(double value)
    {
        await this.SetVolumeAsync(Convert.ToInt32(value));
    }

    public async Task SyncBridgeResolutionAsync(string reason = "manual", bool fullQualityResync = false)
    {
        if (WebView2?.CoreWebView2 is null)
        {
            Logger.WriteWarning($"[CloudGame][Size] SyncBridgeResolution skipped reason={reason}: CoreWebView2=null");
            return;
        }

        var quality = Option.Quality;
        Logger.WriteInfo($"[CloudGame][Size] SyncBridgeResolution begin reason={reason} fullQuality={fullQualityResync}");

        // Resize path: only setGameResolution + keep video playing (matches official page).
        // Full quality resync is reserved for explicit quality changes / fullscreen restore.
        var script = fullQualityResync
            ? $$"""
        (() => {
            const reason = {{System.Text.Json.JsonSerializer.Serialize(reason)}};
            const diag = () => {
                try { return window.__KURO_STREAM_CONTROL__?.getRenderDiagnostic?.(reason) || null; }
                catch (e) { return { error: String(e) }; }
            };
            const before = diag();
            try { window.dispatchEvent(new Event('resize')); } catch (e) {
                return JSON.stringify({ ok:false, stage:'dispatch-resize', error:String(e), before });
            }
            const control = window.__KURO_STREAM_CONTROL__;
            if (!control?.applyQualityProfile) {
                const synced = control?.syncResolution?.();
                return JSON.stringify({ ok:!!synced, stage:'sync-only', before, after:diag() });
            }
            const applied = control.applyQualityProfile({
                bitRate: {{quality.BitRate}},
                bitRateMin: {{quality.BitRateMin}},
                bitRateMax: {{quality.BitRateMax}},
                fps: {{quality.Fps}},
                targetWidth: {{quality.Width}},
                targetHeight: {{quality.Height}},
                streamStrategy: "{{quality.StreamStrategy}}",
                enableImageEnhancement: {{(quality.EnableImageEnhancement ? "true" : "false")}}
            }, {
                resendResolution: true,
                noReport: true,
                reason: reason
            });
            return JSON.stringify({ ok:true, reason, mode:'full-quality', applied, before, after:diag() });
        })();
        """
            : $$"""
        (() => {
            const reason = {{System.Text.Json.JsonSerializer.Serialize(reason)}};
            const control = window.__KURO_STREAM_CONTROL__;
            const sdk = window.__KURO_STREAM_SDK__ || window.WLCG;
            // Do NOT dispatch a synthetic 'resize' here — that re-enters the JS
            // resize listener and double-fires setGameResolution (crash path).
            let resolutionOk = false;
            let res = null;
            try {
                if (control?.syncResolution) {
                    resolutionOk = !!control.syncResolution();
                } else if (sdk && typeof sdk.setGameResolution === 'function') {
                    const dpr = Number(devicePixelRatio) > 0 ? Number(devicePixelRatio) : 1;
                    const w = Math.max(2, Math.round((document.body?.clientWidth || innerWidth || 1280) * dpr));
                    const h = Math.max(2, Math.round((document.body?.clientHeight || innerHeight || 720) * dpr));
                    const even = v => (v % 2 === 0 ? v : v - 1);
                    res = { w: even(w), h: even(h), dpr };
                    sdk.setGameResolution(res.w, res.h);
                    resolutionOk = true;
                }
            } catch (e) {
                return JSON.stringify({ ok:false, stage:'setGameResolution', error:String(e), res });
            }

            try { sdk?.gameVideoPlay?.(); } catch {}
            try { document.getElementById('kuro-stream-surface')?.focus?.(); } catch {}

            return JSON.stringify({
                ok: resolutionOk,
                reason,
                mode: 'resolution-only',
                res
            });
        })();
        """;

        try
        {
            var result = await WebView2.CoreWebView2.ExecuteScriptAsync(script)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5));
            Logger.WriteInfo($"[CloudGame][Size] SyncBridgeResolution end reason={reason} fullQuality={fullQualityResync} result={result}");
        }
        catch (TimeoutException)
        {
            // Task.WaitAsync(TimeSpan) → TimeoutException (not OCE) on .NET 6+.
            Logger.WriteWarning($"[CloudGame][Size] SyncBridgeResolution timeout reason={reason}");
        }
        catch (OperationCanceledException ex)
        {
            // WebView process dying / host closing often surfaces as OCE/TCE here.
            Logger.WriteWarning(
                $"[CloudGame][Size] SyncBridgeResolution canceled reason={reason}: {ex.GetType().Name}: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            Logger.WriteError($"[CloudGame][Size] SyncBridgeResolution failed reason={reason}: {ex}");
        }
    }

    protected override void OnDisposing()
    {
        Logger.WriteInfo("[CloudGame] Dispose begin (will cancel KeepAlive + ViewModel CTS)");
        StopCloudSessionKeepAlive();
        ShowSystemCursor();
        ReleaseWebViewCursorSubclass();
        if (Window is not null)
        {
            Window.Closed -= Window_Closed;
            Window.Activated -= Window_Activated;
        }
        KuroCloudGameContext.CloudGameProcessTracker.OnProgressChanged -=
            CloudGameProcessTracker_OnProgressChanged;
        KuroCloudGameContext.ClearWindow();
        KuroCloudGameContext.CloudGameEventPublisher.Publish(
            new(Waves.Core.Models.Enums.CloudCoreType.None)
        );
        try
        {
            WebView2?.Close();
        }
        catch (Exception ex)
        {
            Logger.WriteWarning($"[CloudGame] WebView2.Close: {ex.GetType().Name}: {ex.Message}");
        }
        this._cursorTimer?.Stop();
        this._cursorTimer = null;
        this._hotkeyTimer?.Stop();
        this._hotkeyTimer = null;
        WebView2 = null;
        Window = null;
        Logger.WriteInfo("[CloudGame] Dispose end");
    }
}
