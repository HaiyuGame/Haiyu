using System;
using System.Collections.Generic;
using System.Text;
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
            this.DelayTime = $"延迟：{message.Detail.NetWorkDelay} ms";
            this.Fps = $"客户端：{message.Detail.Fps}帧";
            this.Network = $"带宽：{message.Detail.Bitrate / 8 / 1024.0:0.#} MB/s";
            this.PacketLossRate = $"丢包率：{message.Detail.PacketLossRate}%";
        });
    }

    [RelayCommand]
    async Task SizeChanged()
    {
        await this.SyncBridgeResolutionAsync();
    }

    async partial void OnVolumeValueChanged(double value)
    {
        await this.SetVolumeAsync(Convert.ToInt32(value));
    }

    public async Task SyncBridgeResolutionAsync()
    {
        if (WebView2?.CoreWebView2 is null)
        {
            return;
        }

        var quality = Option.Quality;
        var script = $$"""
        (() => {
            const control = window.__KURO_STREAM_CONTROL__;
            control?.applyQualityProfile?.({
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
                reason: "size-changed"
            });
        })();
        """;

        try
        {
            await WebView2.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch
        {
        }
    }

    public override void Dispose()
    {
        ShowSystemCursor();
        ReleaseWebViewCursorSubclass();
        if (Window is not null)
        {
            Window.Activated -= Window_Activated;
        }
        WebView2?.Close();
        this._cursorTimer?.Stop();
        this._cursorTimer = null;
        this._hotkeyTimer?.Stop();
        this._hotkeyTimer = null;
    }
}
