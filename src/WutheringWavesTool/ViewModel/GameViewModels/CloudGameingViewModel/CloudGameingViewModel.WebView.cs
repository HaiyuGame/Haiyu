using System;
using System.Collections.Generic;
using System.Text;
using Waves.Api.Models.CloudGame;

namespace Haiyu.ViewModel.GameViewModels
{
    partial class CloudGameingViewModel
    {
        public async Task<bool> RequestExitAsync()
        {
            return await InvokeStreamControlAsync("requestExit");
        }

        public async Task<bool> SetVolumeAsync(int percent)
        {
            return await InvokeStreamControlAsync("setVolume", percent);
        }

        public async Task<bool> SetMutedAsync(bool muted)
        {
            return await InvokeStreamControlAsync("setMuted", muted);
        }

        public async Task<bool> SetImageEnhancementAsync(bool enabled)
        {
            return await InvokeStreamControlAsync("setImageEnhancement", enabled);
        }

        public async Task<bool> ApplyQualityProfileAsync(StreamQualityOptions quality)
        {
            return await InvokeStreamControlAsync(
                "applyQualityProfile",
                new ApplyQualityProfilePayload
                {
                    BitRate = quality.BitRate,
                    BitRateMin = quality.BitRateMin,
                    BitRateMax = quality.BitRateMax,
                    Fps = quality.Fps,
                    Width = quality.Width,
                    Height = quality.Height,
                    CodecType = quality.CodecType,
                    StreamStrategy = quality.StreamStrategy,
                    EnableImageEnhancement = quality.EnableImageEnhancement,
                    Dpi = quality.DPI
                }
            );
        }

        

        private async Task<bool> InvokeStreamControlAsync(string methodName, params object[] args)
        {
            if (WebView2?.CoreWebView2 is null)
            {
                return false;
            }

            var json = methodName switch
            {
                "requestExit" => string.Empty,
                "setVolume" => JsonSerializer.Serialize(args.ElementAtOrDefault(0), CloudGameContext.Default.Int32),
                "setMuted" => JsonSerializer.Serialize(args.ElementAtOrDefault(0), CloudGameContext.Default.Boolean),
                "setImageEnhancement" => JsonSerializer.Serialize(args.ElementAtOrDefault(0), CloudGameContext.Default.Boolean),
                "applyQualityProfile" => JsonSerializer.Serialize((ApplyQualityProfilePayload)args.ElementAtOrDefault(0)!, CloudGameContext.Default.ApplyQualityProfilePayload),
                _ => throw new NotSupportedException(LanguageService.FormatByText(LanguageService.GetStringByText("不支持的串流控制方法: {0}"), methodName))
            };

            var script = methodName switch
            {
                "requestExit" => "window.__KURO_STREAM_CONTROL__?.requestExit?.()",
                _ => $"window.__KURO_STREAM_CONTROL__?.{methodName}?.({json})"
            };

            try
            {
                await WebView2.CoreWebView2.ExecuteScriptAsync(script);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
        }
    }
}
