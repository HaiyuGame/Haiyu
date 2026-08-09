namespace Haiyu.Common.KuroWebView;

public static class CloudGameBuilder
{
    private static string ToJavaScriptString(string value)
    {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n")}\"";
    }

    /// <summary>
    /// 生成串流页面
    /// </summary>
    /// <param name="scriptUrlJson"></param>
    /// <param name="dispatchMessageJson"></param>
    /// <param name="bridgeConfigJson"></param>
    /// <param name="storageItemsJson"></param>
    /// <returns></returns>
    public static string BuildDefaultBridgeHtml(string scriptUrlJson,string dispatchMessageJson,string bridgeConfigJson,string storageItemsJson)
    {
        return $$"""
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Kuro Stream Bridge</title>
    <style>
        :root {
            color-scheme: dark;
            --bg: #05080d;
            --panel: rgba(18, 24, 34, 0.68);
            --border: rgba(255, 255, 255, 0.14);
            --accent: #8fd3ff;
            --accent-strong: #5fb6ff;
            --muted: #a4b7ce;
            --text: #f7fbff;
            --error: #ff8b7f;
            --shadow: 0 24px 80px rgba(0, 0, 0, 0.45);
            --shadow-soft: 0 12px 32px rgba(0, 0, 0, 0.24);
        }

        * {
            box-sizing: border-box;
        }

        html, body {
            width: 100%;
            height: 100%;
            margin: 0;
            background:
                radial-gradient(circle at 15% 18%, rgba(115, 190, 255, 0.2), transparent 28%),
                radial-gradient(circle at 82% 14%, rgba(111, 120, 255, 0.18), transparent 30%),
                radial-gradient(circle at 50% 120%, rgba(34, 91, 201, 0.22), transparent 34%),
                linear-gradient(180deg, #0d131c 0%, #06090f 100%);
            color: var(--text);
            font-family: "Segoe UI", "Microsoft YaHei UI", sans-serif;
            overflow: hidden;
        }

        body {
            position: relative;
        }

        body::before,
        body::after {
            content: "";
            position: absolute;
            pointer-events: none;
            z-index: 0;
            border-radius: 50%;
            filter: blur(22px);
            opacity: 0.72;
        }

        body::before {
            top: -120px;
            left: -120px;
            width: 320px;
            height: 320px;
            background: radial-gradient(circle, rgba(122, 202, 255, 0.18) 0%, rgba(122, 202, 255, 0) 70%);
            animation: kuro-float 18s ease-in-out infinite;
        }

        body::after {
            right: -90px;
            bottom: -120px;
            width: 360px;
            height: 360px;
            background: radial-gradient(circle, rgba(88, 112, 255, 0.16) 0%, rgba(88, 112, 255, 0) 72%);
            animation: kuro-float 22s ease-in-out infinite reverse;
        }

        body.error #bridge-message {
            color: var(--error);
        }

        #kuro-stream-surface {
            position: absolute;
            inset: 0;
            width: 100%;
            height: 100%;
            background: #000;
            cursor: auto;
            z-index: 1;
            overflow: hidden;
        }

        #kuro-stream-surface * {
            cursor: inherit !important;
        }

        /* Window resize must only reflow CSS — never force a new encode size. */
        #bridge-overlay {
            position: absolute;
            inset: 0;
            display: flex;
            align-items: center;
            justify-content: center;
            pointer-events: none;
            z-index: 20;
            padding: 24px;
            background: linear-gradient(180deg, rgba(2, 6, 12, 0.12), rgba(2, 6, 12, 0.28));
            transition: opacity 240ms ease, transform 320ms cubic-bezier(0.2, 0.9, 0.2, 1), filter 320ms ease;
        }

        #bridge-overlay.hidden {
            opacity: 0;
            transform: scale(1.015);
            filter: blur(8px);
        }

        #bridge-card {
            position: relative;
            width: min(580px, calc(100vw - 48px));
            padding: 30px 30px 26px;
            border-radius: 28px;
            overflow: hidden;
            background:
                linear-gradient(180deg, rgba(255, 255, 255, 0.08), rgba(255, 255, 255, 0.02)),
                var(--panel);
            border: 1px solid var(--border);
            -webkit-backdrop-filter: blur(30px) saturate(160%);
            backdrop-filter: blur(30px) saturate(160%);
            box-shadow: var(--shadow), inset 0 1px 0 rgba(255, 255, 255, 0.08);
            animation: kuro-card-in 420ms cubic-bezier(0.16, 1, 0.3, 1);
        }

        #bridge-card::before,
        #bridge-card::after {
            content: "";
            position: absolute;
            inset: 0;
            pointer-events: none;
        }

        #bridge-card::before {
            inset: 1px;
            border-radius: 27px;
            border: 1px solid rgba(255, 255, 255, 0.06);
        }

        #bridge-card::after {
            inset: -35% auto auto -10%;
            width: 62%;
            height: 52%;
            background: radial-gradient(circle, rgba(143, 211, 255, 0.2) 0%, rgba(143, 211, 255, 0) 72%);
            filter: blur(10px);
            opacity: 0.95;
        }

        #bridge-header {
            position: relative;
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 16px;
            z-index: 1;
        }

        #bridge-status {
            display: inline-flex;
            align-items: center;
            gap: 10px;
            padding: 8px 12px;
            border-radius: 999px;
            background: rgba(255, 255, 255, 0.06);
            border: 1px solid rgba(255, 255, 255, 0.08);
            box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.05);
            color: #dcecff;
            font-size: 12px;
            letter-spacing: 0.04em;
        }

        #bridge-status::before {
            content: "";
            width: 8px;
            height: 8px;
            border-radius: 50%;
            background: radial-gradient(circle, #b6ebff 0%, var(--accent-strong) 65%, rgba(95, 182, 255, 0.2) 100%);
            box-shadow: 0 0 0 5px rgba(95, 182, 255, 0.12), 0 0 18px rgba(95, 182, 255, 0.4);
            animation: kuro-pulse 1.8s ease-in-out infinite;
        }

        #bridge-tag {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            padding: 8px 12px;
            border-radius: 999px;
            background: linear-gradient(180deg, rgba(58, 105, 162, 0.35), rgba(24, 54, 91, 0.32));
            border: 1px solid rgba(126, 200, 255, 0.22);
            color: var(--accent);
            font-size: 12px;
            letter-spacing: 0.08em;
            text-transform: uppercase;
            box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.08);
        }

        #bridge-tag::before {
            content: "";
            width: 6px;
            height: 6px;
            border-radius: 50%;
            background: currentColor;
            box-shadow: 0 0 12px currentColor;
        }

        #bridge-title {
            position: relative;
            z-index: 1;
            margin: 18px 0 10px;
            font-size: 30px;
            font-weight: 700;
            letter-spacing: -0.03em;
            text-shadow: 0 8px 28px rgba(0, 0, 0, 0.3);
        }

        #bridge-message {
            position: relative;
            z-index: 1;
            margin: 0;
            color: var(--muted);
            font-size: 14px;
            line-height: 1.7;
        }

        #bridge-region {
            position: relative;
            z-index: 1;
            display: inline-flex;
            align-items: center;
            gap: 8px;
            margin-top: 18px;
            padding: 10px 14px;
            border-radius: 16px;
            background: rgba(255, 255, 255, 0.045);
            border: 1px solid rgba(255, 255, 255, 0.08);
            box-shadow: var(--shadow-soft), inset 0 1px 0 rgba(255, 255, 255, 0.05);
            color: #dbe7f5;
            font-size: 13px;
        }

        #bridge-region::before {
            content: "";
            width: 14px;
            height: 14px;
            border-radius: 50%;
            background:
                radial-gradient(circle at 35% 35%, rgba(255, 255, 255, 0.9), rgba(255, 255, 255, 0) 36%),
                linear-gradient(180deg, rgba(143, 211, 255, 0.95), rgba(75, 135, 255, 0.95));
            box-shadow: 0 0 18px rgba(95, 182, 255, 0.34);
            flex: 0 0 auto;
        }

        #bridge-progress {
            position: relative;
            width: 100%;
            height: 10px;
            margin-top: 24px;
            overflow: hidden;
            border-radius: 999px;
            background: rgba(255, 255, 255, 0.08);
            box-shadow: inset 0 1px 2px rgba(0, 0, 0, 0.32);
            z-index: 1;
        }

        #bridge-progress::before {
            content: "";
            position: absolute;
            inset: 1px;
            border-radius: inherit;
            background: linear-gradient(180deg, rgba(255, 255, 255, 0.08), rgba(255, 255, 255, 0.01));
        }

        #bridge-progress::after {
            content: "";
            position: absolute;
            top: 1px;
            bottom: 1px;
            left: 0;
            width: 34%;
            background: linear-gradient(90deg, rgba(90, 129, 255, 0.12) 0%, #4b8cff 20%, #99e4ff 52%, #5fb6ff 82%, rgba(143, 211, 255, 0.15) 100%);
            box-shadow: 0 0 18px rgba(95, 182, 255, 0.42);
            animation: kuro-progress 1.35s cubic-bezier(0.4, 0, 0.2, 1) infinite;
            border-radius: inherit;
        }

        #bridge-footer {
            position: relative;
            z-index: 1;
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 14px;
            margin-top: 14px;
            color: rgba(226, 238, 250, 0.72);
            font-size: 12px;
        }

        #bridge-footer-hint {
            white-space: nowrap;
        }

        #bridge-loading-dots {
            display: inline-flex;
            align-items: center;
            gap: 6px;
        }

        #bridge-loading-dots span {
            width: 5px;
            height: 5px;
            border-radius: 50%;
            background: rgba(226, 238, 250, 0.46);
            animation: kuro-dots 1.2s ease-in-out infinite;
        }

        #bridge-loading-dots span:nth-child(2) {
            animation-delay: 0.15s;
        }

        #bridge-loading-dots span:nth-child(3) {
            animation-delay: 0.3s;
        }

        @keyframes kuro-progress {
            from { transform: translateX(-125%); }
            to { transform: translateX(335%); }
        }

        @keyframes kuro-card-in {
            from {
                opacity: 0;
                transform: translateY(18px) scale(0.985);
            }

            to {
                opacity: 1;
                transform: translateY(0) scale(1);
            }
        }

        @keyframes kuro-float {
            0%,
            100% {
                transform: translate3d(0, 0, 0) scale(1);
            }

            50% {
                transform: translate3d(12px, 18px, 0) scale(1.06);
            }
        }

        @keyframes kuro-pulse {
            0%,
            100% {
                transform: scale(1);
                opacity: 0.85;
            }

            50% {
                transform: scale(1.16);
                opacity: 1;
            }
        }

        @keyframes kuro-dots {
            0%,
            80%,
            100% {
                transform: translateY(0);
                opacity: 0.35;
            }

            40% {
                transform: translateY(-4px);
                opacity: 1;
            }
        }

        @media (max-width: 640px) {
            #bridge-overlay {
                align-items: flex-end;
                padding: 16px;
            }

            #bridge-card {
                width: 100%;
                padding: 24px 22px 22px;
                border-radius: 24px;
            }

            #bridge-header,
            #bridge-footer {
                flex-direction: column;
                align-items: flex-start;
            }

            #bridge-title {
                font-size: 24px;
            }

            #bridge-footer-hint {
                white-space: normal;
            }
        }

        @media (prefers-reduced-motion: reduce) {
            *,
            *::before,
            *::after {
                animation-duration: 0.01ms !important;
                animation-iteration-count: 1 !important;
                transition-duration: 0.01ms !important;
                scroll-behavior: auto !important;
            }
        }
    </style>
</head>
<body>
    <div id="kuro-stream-surface" tabindex="0"></div>
    <div id="bridge-overlay">
        <div id="bridge-card">
            <div id="bridge-header">
                <div id="bridge-tag">Native Stream Bridge</div>
                <div id="bridge-status">{{LanguageService.GetStringByText("正在建立安全连接")}}</div>
            </div>
            <div id="bridge-title">{{LanguageService.GetStringByText("正在接入云端实例")}}</div>
            <p id="bridge-message">{{LanguageService.GetStringByText("原生业务层已完成开始游戏，正在初始化 Welink 串流 SDK。")}}</p>
            <div id="bridge-region">{{LanguageService.GetStringByText("节点：测试 | 会话：测试1")}}</div>
            <div id="bridge-progress"></div>
            <div id="bridge-footer">
                <div id="bridge-footer-hint">{{LanguageService.GetStringByText("正在同步画面、输入与音频通道")}}</div>
                <div id="bridge-loading-dots" aria-hidden="true">
                    <span></span>
                    <span></span>
                    <span></span>
                </div>
            </div>
        </div>
    </div>

    <script>
        (() => {
            let sdk = null;
            let isForeground = false;
            let lastNetworkStat = null;
            let mediaObserver = null;
            const audioState = {
                level: 1,
                muted: false
            };

            const enhancementState = {
                enabled: false
            };
            const payload = {
                scriptUrl: {{scriptUrlJson}},
                dispatchMessage: {{dispatchMessageJson}},
                bridgeConfig: {{bridgeConfigJson}},
                storageItems: {{storageItemsJson}}
            };

            enhancementState.enabled = Boolean(payload?.bridgeConfig?.enableImageEnhancement);

            const overlay = document.getElementById("bridge-overlay");
            const messageElement = document.getElementById("bridge-message");
            const surface = document.getElementById("kuro-stream-surface");

            const normalizeVolume = (percent) => {
                const parsed = Number(percent);
                if (!Number.isFinite(parsed)) {
                    return 100;
                }

                return Math.max(0, Math.min(100, Math.round(parsed)));
            };

            const applyMediaAudioState = (root = document) => {
                const volume = Math.max(0, Math.min(1, audioState.level));
                const mediaElements = [];

                if (root instanceof HTMLMediaElement) {
                    mediaElements.push(root);
                }

                if (typeof root?.querySelectorAll === "function") {
                    mediaElements.push(...root.querySelectorAll("video, audio"));
                }

                for (const mediaElement of mediaElements) {
                    try {
                        mediaElement.defaultMuted = audioState.muted;
                        mediaElement.muted = audioState.muted;
                        mediaElement.volume = volume;
                    } catch {
                    }
                }
            };

            const ensureMediaObserver = () => {
                if (mediaObserver || !document.documentElement) {
                    return;
                }

                mediaObserver = new MutationObserver((mutations) => {
                    for (const mutation of mutations) {
                        for (const node of mutation.addedNodes) {
                            if (node instanceof HTMLMediaElement) {
                                applyMediaAudioState(node);
                                attachFirstFrameFallback(node);
                                continue;
                            }

                            if (node instanceof Element) {
                                applyMediaAudioState(node);
                                for (const mediaElement of node.querySelectorAll("video")) {
                                    attachFirstFrameFallback(mediaElement);
                                }
                            }
                        }
                    }
                });

                mediaObserver.observe(document.documentElement, {
                    childList: true,
                    subtree: true
                });
            };

            const setStreamVolume = (percent) => {
                const nextPercent = normalizeVolume(percent);
                audioState.level = nextPercent / 100;
                audioState.muted = nextPercent <= 0 ? true : audioState.muted;
                ensureMediaObserver();
                applyMediaAudioState(document);
                return { level: nextPercent, muted: audioState.muted };
            };

            const setStreamMuted = (muted) => {
                audioState.muted = Boolean(muted);
                ensureMediaObserver();
                applyMediaAudioState(document);
                return { level: Math.round(audioState.level * 100), muted: audioState.muted };
            };

            const applyImageEnhancement = (enabled) => {
                // Hard-disable enhance in WebView2 bridge. openSuperResolution / openEnhance
                // have been present in the crash window (D3D11 video path).
                enhancementState.enabled = false;
                post("image-enhancement", {
                    message: "WebView2 bridge keeps image enhancement off",
                    enabled: false,
                    method: "disabled"
                });
                return { enabled: false, applied: false, method: "disabled" };
            };

            const updateBridgeQualityConfig = (config) => {
                if (!config || typeof config !== "object") {
                    return payload.bridgeConfig;
                }

                payload.bridgeConfig = {
                    ...payload.bridgeConfig,
                    ...config
                };
                enhancementState.enabled = Boolean(payload.bridgeConfig.enableImageEnhancement);
                return payload.bridgeConfig;
            };

            const applyQualityProfile = (config, options = {}) => {
                const nextConfig = updateBridgeQualityConfig(config);
                const reason = options.reason || "applyQualityProfile";

                if (!sdk) {
                    post("diagnostic", {
                        reason: `${reason}:no-sdk`,
                        applied: false
                    });
                    return { applied: false, config: nextConfig };
                }

                const noReport = Boolean(options.noReport);
                const resolution = getPhysicalResolution();
                const configuredBitRate = Number(nextConfig.bitRate) || 18000;
                const resolutionBitRate = Math.max(
                    6000,
                    Math.round(resolution.width * resolution.height * 0.010)
                );
                const targetBitRate = Math.min(configuredBitRate, resolutionBitRate);
                const configuredMinBitRate = Number(nextConfig.bitRateMin) || 0;
                const minBitRate = Math.min(configuredMinBitRate, targetBitRate);
                const configuredMaxBitRate = Number(nextConfig.bitRateMax) || configuredBitRate;
                const maxBitRate = Math.min(configuredMaxBitRate, targetBitRate);
                const streamStrategy = nextConfig.streamStrategy;

                post("diagnostic", {
                    reason: `${reason}:begin`,
                    resolution,
                    bitRate: targetBitRate,
                    minBitRate,
                    maxBitRate,
                    fps: nextConfig.fps || 60,
                    streamStrategy,
                    resendResolution: Boolean(options.resendResolution),
                    firstFramePresented
                });

                if (typeof sdk?.setStreamStrategy === "function" && streamStrategy !== undefined && streamStrategy !== null) {
                    try {
                        sdk.setStreamStrategy(String(streamStrategy));
                    } catch (error) {
                        post("warning", {
                            message: "setStreamStrategy failed",
                            detail: error?.message || String(error)
                        });
                    }
                }

                if (typeof sdk?.setBitrateRange === "function" && minBitRate > 0 && maxBitRate > 0) {
                    try {
                        sdk.setBitrateRange(minBitRate, maxBitRate, noReport);
                    } catch (error) {
                        post("warning", {
                            message: "setBitrateRange failed",
                            detail: error?.message || String(error)
                        });
                    }
                } else if (typeof sdk?.setBitrate === "function") {
                    try {
                        sdk.setBitrate(targetBitRate, noReport);
                    } catch (error) {
                        post("warning", {
                            message: "setBitrate failed",
                            detail: error?.message || String(error)
                        });
                    }
                }

                if (typeof sdk?.setFps === "function") {
                    try {
                        sdk.setFps(nextConfig.fps || 60);
                    } catch (error) {
                        post("warning", {
                            message: "setFps failed",
                            detail: error?.message || String(error)
                        });
                    }
                }

                // Never re-open super-resolution from quality/first-frame paths.
                // Chromium logs showed H264 decoder backup then BrowserProcessExited;
                // openSuperResolution + setGameResolution thrash correlates with that.
                if (options.applyEnhancement === true) {
                    applyImageEnhancement(enhancementState.enabled);
                }

                // Only push resolution when explicitly requested or size really changed.
                let resolutionOk = true;
                if (options.resendResolution || options.forceResolution) {
                    resolutionOk = setGameResolution();
                }

                // NO delayed second setGameResolution — that 3s resend after first-frame
                // recreated VideoReceiveStream and preceded the last BrowserProcessExited.

                post("diagnostic", {
                    reason: `${reason}:end`,
                    resolutionOk,
                    resolution: getPhysicalResolution()
                });

                return {
                    applied: true,
                    resolutionOk,
                    config: nextConfig,
                    resolution,
                    bitRate: targetBitRate,
                    minBitRate,
                    maxBitRate
                };
            };

            const requestExit = () => {
                const candidateNames = ["exitGame", "stopGame", "closeGame", "endGame", "destroy"];

                for (const candidateName of candidateNames) {
                    if (typeof sdk?.[candidateName] !== "function") {
                        continue;
                    }

                    try {
                        sdk[candidateName]();
                        post("status", { message: {{ToJavaScriptString(LanguageService.GetStringByText("已调用退出方法，准备退出当前会话："))}} + candidateName });
                        return { invoked: true, method: candidateName };
                    } catch (error) {
                        post("warning", {
                            message: {{ToJavaScriptString(LanguageService.GetStringByText("调用退出方法失败："))}} + candidateName,
                            detail: error?.message || String(error)
                        });
                    }
                }

                post("warning", { message: {{ToJavaScriptString(LanguageService.GetStringByText("Welink 未暴露可用的退出方法，将由宿主直接关闭窗口。"))}} });
                return { invoked: false };
            };

            // Host-side controls exposed to the native window.
            // layoutSurface: CSS-only fit after window resize (safe).
            // syncResolution: intentionally does NOT call setGameResolution on resize
            // path from host SizeChanged — that renegotiates WebRTC and crashes WebView2.
            window.__KURO_STREAM_CONTROL__ = {
                setVolume: setStreamVolume,
                setMuted: setStreamMuted,
                setImageEnhancement: applyImageEnhancement,
                applyQualityProfile,
                syncResolution: () => false,
                layoutSurface: (reason) => layoutSurface(reason || "layout"),
                requestExit,
                getLastNetworkStat: () => lastNetworkStat
            };

            ensureMediaObserver();

            if (document.body) {
                document.body.tabIndex = 0;
            }

            const post = (type, extra = {}) => {
                try {
                    window.chrome?.webview?.postMessage({ type, ...extra });
                } catch {
                }
            };

            let focusSurfaceLock = false;
            const focusSurface = () => {
                if (focusSurfaceLock) {
                    return;
                }

                focusSurfaceLock = true;
                try {
                    surface?.focus?.();
                } catch {
                } finally {
                    queueMicrotask(() => {
                        focusSurfaceLock = false;
                    });
                }
            };

            const setMessage = (message, level = "info") => {
                if (messageElement) {
                    messageElement.textContent = message;
                }

                document.body.classList.toggle("error", level === "error");
                post("status", { message, level });
            };

            const parseJson = (value) => {
                if (!value || typeof value !== "string") {
                    return null;
                }

                try {
                    return JSON.parse(value);
                } catch {
                    return null;
                }
            };

            const launchState = payload.storageItems || window.__KURO_LAUNCH_OPTIONS__?.storageItems || {};
            let preopenState = 0;
            let keepAliveTimer = null;
            let lastPreLaunchResolution = null;
            let firstFramePresented = false;

            const readStateItem = (key) => {
                try {
                    const localValue = window.localStorage?.getItem?.(key);
                    if (localValue) {
                        return localValue;
                    }
                } catch {
                }

                try {
                    const sessionValue = window.sessionStorage?.getItem?.(key);
                    if (sessionValue) {
                        return sessionValue;
                    }
                } catch {
                }

                const launchValue = launchState[key];
                return typeof launchValue === "string" && launchValue.length > 0
                    ? launchValue
                    : null;
            };

            const decodePayload = (value) => {
                try {
                    if (typeof value === "string") {
                        return value;
                    }

                    if (value instanceof ArrayBuffer) {
                        return new TextDecoder().decode(value);
                    }

                    if (ArrayBuffer.isView(value)) {
                        return new TextDecoder().decode(value);
                    }

                    return String(value ?? "");
                } catch {
                    return "";
                }
            };

            // Official web client: body.clientWidth * devicePixelRatio.
            // Bridge surface fills the host window, so prefer surface metrics
            // with body/inner fallbacks.
            const getViewportResolution = () => ({
                width: Math.max(1, Math.round(
                    surface?.clientWidth
                    || document.body?.clientWidth
                    || window.innerWidth
                    || 1280
                )),
                height: Math.max(1, Math.round(
                    surface?.clientHeight
                    || document.body?.clientHeight
                    || window.innerHeight
                    || 720
                ))
            });

            // Welink renegotiates the video track on setGameResolution; odd or
            // out-of-range values can leave a blank surface after window resize.
            const clampEven = (value, min, max) => {
                const rounded = Number.isFinite(value) ? Math.round(value) : min;
                const clamped = Math.max(min, Math.min(max, rounded));
                if (clamped <= min) {
                    return min % 2 === 0 ? min : min + 1;
                }

                return clamped % 2 === 0 ? clamped : clamped - 1;
            };

            const normalizeResolution = (width, height) => ({
                width: clampEven(width, 640, 3840),
                height: clampEven(height, 360, 2160)
            });

            const getPhysicalResolution = () => {
                const viewport = getViewportResolution();
                const scale = Number(window.devicePixelRatio) > 0 ? Number(window.devicePixelRatio) : 1;
                return {
                    width: viewport.width * scale,
                    height: viewport.height * scale,
                    viewportWidth: viewport.width,
                    viewportHeight: viewport.height,
                    scale
                };
            };

            let resolutionResizeTimer = 0;
            let lastSyncedResolutionKey = "";

            const styleSnapshot = (el) => {
                if (!el) {
                    return null;
                }

                const s = getComputedStyle(el);
                const rect = el.getBoundingClientRect?.();
                return {
                    display: s.display,
                    visibility: s.visibility,
                    opacity: s.opacity,
                    zIndex: s.zIndex,
                    transform: s.transform,
                    width: s.width,
                    height: s.height,
                    rect: rect
                        ? {
                            x: Math.round(rect.x),
                            y: Math.round(rect.y),
                            w: Math.round(rect.width),
                            h: Math.round(rect.height)
                        }
                        : null
                };
            };

            const collectRenderDiagnostic = (reason) => {
                const surfaceEl = document.getElementById("kuro-stream-surface") || surface;
                const videos = [...document.querySelectorAll("video")].map((x) => ({
                    id: x.id,
                    ready: x.readyState,
                    network: x.networkState,
                    w: x.videoWidth,
                    h: x.videoHeight,
                    cw: x.clientWidth,
                    ch: x.clientHeight,
                    paused: x.paused,
                    ended: x.ended,
                    muted: x.muted,
                    volume: x.volume,
                    currentTime: Number(x.currentTime?.toFixed?.(2) ?? x.currentTime),
                    srcObject: Boolean(x.srcObject),
                    trackCount: x.srcObject?.getVideoTracks?.()?.length ?? 0,
                    trackStates: (x.srcObject?.getVideoTracks?.() || []).map((t) => ({
                        id: t.id,
                        enabled: t.enabled,
                        muted: t.muted,
                        readyState: t.readyState
                    })),
                    style: styleSnapshot(x)
                }));
                const canvases = [...document.querySelectorAll("canvas")].map((x) => ({
                    id: x.id,
                    w: x.width,
                    h: x.height,
                    cw: x.clientWidth,
                    ch: x.clientHeight,
                    style: styleSnapshot(x)
                }));

                return {
                    reason,
                    ts: Date.now(),
                    href: location.href,
                    visibility: document.visibilityState,
                    focused: document.hasFocus(),
                    activeElement: document.activeElement
                        ? (document.activeElement.id || document.activeElement.tagName)
                        : null,
                    inner: [window.innerWidth, window.innerHeight],
                    body: [document.body?.clientWidth || 0, document.body?.clientHeight || 0],
                    surface: surfaceEl
                        ? [surfaceEl.clientWidth, surfaceEl.clientHeight]
                        : null,
                    surfaceStyle: styleSnapshot(surfaceEl),
                    overlayHidden: document.getElementById("bridge-overlay")?.classList?.contains("hidden") ?? null,
                    dpr: window.devicePixelRatio,
                    pointerLock: Boolean(document.pointerLockElement),
                    firstFramePresented,
                    hasSdk: Boolean(sdk || window.__KURO_STREAM_SDK__),
                    physical: getPhysicalResolution(),
                    lastSyncedResolutionKey,
                    videoCount: videos.length,
                    canvasCount: canvases.length,
                    videos,
                    canvases
                };
            };

            if (window.__KURO_STREAM_CONTROL__) {
                window.__KURO_STREAM_CONTROL__.getRenderDiagnostic = collectRenderDiagnostic;
            }

            // PROBE: window resize does nothing except post a log line.
            // Host SizeChanged also does nothing. If crash still happens on maximize,
            // it is NOT from Haiyu SizeChanged / setGameResolution / layoutSurface.
            let lastSetGameResolutionAt = 0;
            let jsResizeProbeSeq = 0;
            const layoutSurface = (reason) => {
                const n = ++jsResizeProbeSeq;
                clearTimeout(resolutionResizeTimer);
                resolutionResizeTimer = setTimeout(() => {
                    const resolution = getPhysicalResolution();
                    let ok = false;
                    try {
                        if (sdk && typeof sdk.setGameResolution === "function") {
                            sdk.setGameResolution(resolution.width, resolution.height);
                            lastSetGameResolutionAt = Date.now();
                            lastSyncedResolutionKey = `${resolution.width}x${resolution.height}`;
                            ok = true;
                        }
                    } catch (error) {
                        post("warning", { message: `setGameResolution failed: ${String(error)}` });
                    }
                    post("resolution-sync", {
                        reason,
                        seq: n,
                        ok,
                        width: resolution.width,
                        height: resolution.height,
                        scale: resolution.scale,
                        action: "setGameResolution"
                    });
                }, 100);
                return true;
            };

            if (window.__KURO_STREAM_CONTROL__) {
                window.__KURO_STREAM_CONTROL__.layoutSurface = layoutSurface;
                window.__KURO_STREAM_CONTROL__.syncResolution = () => layoutSurface("control-sync");
            }

            window.addEventListener("resize", () => layoutSurface("js-resize"), false);
            window.addEventListener("orientationchange", () => layoutSurface("js-orientation"), false);

            const getSdkLoginInfo = () => {
                const appStore = parseJson(readStateItem("useMcCloudGameAppStore"));
                const directLoginInfo = parseJson(readStateItem("sdkLoginInfo"));
                return appStore?.sdkLoginInfo || directLoginInfo || null;
            };

            const getHeartbeatConfig = () => {
                const sdkConfig = parseJson(readStateItem("__KrSDK_SYS_CONF__")) || {};
                const enabled = Number(sdkConfig?.heartEnable ?? 1) !== 0;
                const frequencySeconds = Math.max(5, Number(sdkConfig?.heartFreq || 5) || 5);
                return {
                    enabled,
                    frequencySeconds,
                    intervalMs: frequencySeconds * 1000
                };
            };

            const getUserLoginInfo = () => {
                const sdkLoginInfo = getSdkLoginInfo();
                const traceId = readStateItem("useMcCloudGameDid") || "";

                if (!sdkLoginInfo?.cuid || !sdkLoginInfo?.token || !sdkLoginInfo?.username || !traceId) {
                    post("warning", {
                        message: {{ToJavaScriptString(LanguageService.GetStringByText("桥页登录态不完整，无法生成登录信息。"))}},
                        detail: {
                            hasSdkLoginInfo: Boolean(sdkLoginInfo),
                            hasUid: Boolean(sdkLoginInfo?.cuid),
                            hasToken: Boolean(sdkLoginInfo?.token),
                            hasUserName: Boolean(sdkLoginInfo?.username),
                            hasTraceId: Boolean(traceId)
                        }
                    });
                    return null;
                }

                return {
                    LoginCode: 1,
                    Uid: sdkLoginInfo.cuid,
                    Token: sdkLoginInfo.token,
                    UserName: sdkLoginInfo.username,
                    TraceId: traceId
                };
            };

            const sendMessageWithKey = (key, data) => {
                if (!sdk || typeof sdk.sendDataToGameWithKey !== "function") {
                    post("warning", { message: {{ToJavaScriptString(LanguageService.GetStringByText("Welink 缺少回传方法，无法回传："))}} + key });
                    return false;
                }

                try {
                    sdk.sendDataToGameWithKey(data, key);
                    post("game-send", {
                        key,
                        message: typeof data === "string" ? data.slice(0, 400) : String(data)
                    });
                    return true;
                } catch (error) {
                    post("warning", {
                        message: {{ToJavaScriptString(LanguageService.GetStringByText("向游戏回传失败："))}} + key,
                        key,
                        detail: error?.message || String(error)
                    });
                    return false;
                }
            };

            const sendUserData = () => {
                if (preopenState === 1) {
                    return false;
                }

                const loginInfo = getUserLoginInfo();
                if (!loginInfo) {
                    post("warning", { message: {{ToJavaScriptString(LanguageService.GetStringByText("缺少登录态，无法响应登录请求。"))}} });
                    return false;
                }

                return sendMessageWithKey("OnCloudGameLogin", JSON.stringify(loginInfo));
            };

            const sendGamePadDeviceChangeData = () => {
                return sendMessageWithKey("OnGamePadDeviceChange", JSON.stringify({
                    IsConnectPad: 0,
                    VendorId: 0,
                    ProductId: 0
                }));
            };

            const setGameResolution = (width, height) => {
                if (!sdk || typeof sdk.setGameResolution !== "function") {
                    post("diagnostic", {
                        reason: "setGameResolution:unavailable",
                        hasSdk: Boolean(sdk),
                        hasMethod: typeof sdk?.setGameResolution === "function"
                    });
                    return false;
                }

                const target = width > 0 && height > 0
                    ? normalizeResolution(width, height)
                    : getPhysicalResolution();
                const targetWidth = target.width;
                const targetHeight = target.height;
                const key = `${targetWidth}x${targetHeight}`;
                if (key === lastSyncedResolutionKey) {
                    return true;
                }

                try {
                    sdk.setGameResolution(targetWidth, targetHeight);
                    lastSyncedResolutionKey = key;
                    lastSetGameResolutionAt = performance.now();
                    // Compact post only — full DOM snapshots on every resolution
                    // change flooded host IPC and correlated with process death.
                    post("game-resolution", {
                        message: key,
                        width: targetWidth,
                        height: targetHeight,
                        viewportWidth: target.viewportWidth,
                        viewportHeight: target.viewportHeight,
                        scale: target.scale
                    });
                    return true;
                } catch (error) {
                    post("warning", {
                        message: {{ToJavaScriptString(LanguageService.GetStringByText("设置游戏分辨率失败："))}} + key,
                        detail: error?.message || String(error)
                    });
                    return false;
                }
            };

            const sendPreLaunchUserData = (screenResolution) => {
                if (preopenState !== 1) {
                    return false;
                }

                if (screenResolution?.width > 0 && screenResolution?.height > 0) {
                    lastPreLaunchResolution = {
                        width: screenResolution.width,
                        height: screenResolution.height
                    };
                }

                const loginInfo = getUserLoginInfo();
                if (!loginInfo) {
                    post("warning", { message: {{ToJavaScriptString(LanguageService.GetStringByText("缺少预开模式登录态，无法回传预启动信息。"))}} });
                    return false;
                }

                const physical = getPhysicalResolution();
                const qualityFps = payload.bridgeConfig.fps || 60;
                const qualityDpi = Number(payload.bridgeConfig.dpi) || 120;
                const payloadData = {
                    TraceId: loginInfo.TraceId,
                    Platform: "Windows",
                    Fps: qualityFps,
                    Dpi: qualityDpi,
                    DeviceResolution: {
                        Width: physical.width,
                        Height: physical.height
                    },
                    ScreenResolution: {
                        Width: screenResolution?.width || physical.width,
                        Height: screenResolution?.height || physical.height
                    },
                    Device: navigator.userAgent,
                    LoginInfo: loginInfo
                };

                sendMessageWithKey("SetIsWebPlatform", "1");
                return sendMessageWithKey("OnCloudGameLoginPreLaunch", JSON.stringify(payloadData));
            };

            const sendKeepAlive = (reason) => {
                const heartbeatConfig = getHeartbeatConfig();
                if (!heartbeatConfig.enabled || !sdk) {
                    return false;
                }

                let sentWebPlatform = false;
                let sentLogin = false;

                if (preopenState === 1) {
                    sentLogin = sendPreLaunchUserData(lastPreLaunchResolution);
                    sentWebPlatform = sentLogin;
                } else {
                    sentWebPlatform = sendMessageWithKey("SetIsWebPlatform", "1");
                    sentLogin = sendUserData();
                }

                post("keepalive", {
                    reason,
                    preopenState,
                    sentWebPlatform,
                    sentLogin
                });

                return sentWebPlatform || sentLogin;
            };

            const startKeepAliveLoop = () => {
                const heartbeatConfig = getHeartbeatConfig();
                if (!heartbeatConfig.enabled || keepAliveTimer) {
                    return;
                }

                keepAliveTimer = window.setInterval(() => {
                    sendKeepAlive("timer");
                }, heartbeatConfig.intervalMs);

                post("keepalive-config", {
                    enabled: true,
                    frequencySeconds: heartbeatConfig.frequencySeconds
                });

                sendKeepAlive("initial");
            };

            const stopKeepAliveLoop = () => {
                if (!keepAliveTimer) {
                    return;
                }

                window.clearInterval(keepAliveTimer);
                keepAliveTimer = null;
                post("keepalive-stop", { message: {{ToJavaScriptString(LanguageService.GetStringByText("桥页保活已停止。"))}} });
            };

            const handleGameMessage = (message) => {
                post("game-message", { message });

                switch (message) {
                    case "RequestLogin":
                        sendUserData();
                        break;
                    case "RequestGamePadDevice":
                        sendGamePadDeviceChangeData();
                        break;
                    case "HotPatchEnterGame":
                        handleFirstVideoFrame();
                        post("status", { message: "HotPatchEnterGame: playable" });
                        break;
                    case "HotPatchExitGame":
                        setMessage({{ToJavaScriptString(LanguageService.GetStringByText("游戏仍在加载资源，等待进入游戏场景……"))}});
                        break;
                }
            };

            const handleGameMessageWithKey = (key, message) => {
                post("game-message-with-key", { key, message });

                switch (key) {
                    case "InitPostWebView":
                        // Intentionally no setGameResolution — stream already negotiated.
                        break;
                    case "ExitGame":
                        setMessage({{ToJavaScriptString(LanguageService.GetStringByText("云端游戏要求退出当前会话。"))}}, "error");
                        break;
                }
            };

            const handleSdkMessage = (code, detail) => {
                post("sdk-message", { code, detail });

                switch (Number(code)) {
                    case 6038:
                        if (typeof sdk?.setExitPointTimeout === "function") {
                            try {
                                sdk.setExitPointTimeout(100);
                            } catch {
                            }
                        }

                        // Bitrate only — do not re-setGameResolution here (decoder already live).
                        applyQualityProfile(payload.bridgeConfig, {
                            reason: "sdk-6038",
                            resendResolution: false,
                            applyEnhancement: false
                        });
                        hideOverlay();
                        focusSurface();
                        notifyForeground("sdk-first-video-frame");
                        post("first-frame");
                        break;
                    case 6252:
                        if (detail?.pipeState === 1) {
                            sendPreLaunchUserData();
                        }
                        break;
                    case 6253: {
                        const match = typeof detail === "string"
                            ? detail.match(/(\d+)x(\d+)/)
                            : null;

                        if (match) {
                            sendPreLaunchUserData({
                                width: Number(match[1]),
                                height: Number(match[2])
                            });
                            break;
                        }

                        sendPreLaunchUserData();
                        break;
                    }
                    case 6254:
                        if (detail?.type === "preopen" && typeof detail.info === "number") {
                            preopenState = detail.info;
                        }
                        break;
                }
            };

            const notifyForeground = (reason) => {
                if (isForeground) {
                    return;
                }

                if (!sdk || typeof sdk.onResume !== "function") {
                    return;
                }

                try {
                    isForeground = true;
                    sdk.onResume();
                    post("status", { message: {{ToJavaScriptString(LanguageService.GetStringByText("Welink 已切回前台状态："))}} + reason });
                } catch {
                    isForeground = false;
                }
            };

            const syncForegroundFlag = () => {
                isForeground = !document.hidden;
            };

            const notifyBackground = (reason) => {
                if (!isForeground) {
                    return;
                }

                if (!sdk || typeof sdk.onPause !== "function") {
                    return;
                }

                try {
                    isForeground = false;
                    sdk.onPause();
                    post("status", { message: {{ToJavaScriptString(LanguageService.GetStringByText("Welink 已切到后台状态："))}} + reason });
                } catch {
                    isForeground = true;
                }
            };

            const hideOverlay = () => {
                overlay?.classList.add("hidden");
                setTimeout(() => overlay?.remove(), 260);
            };

            const handleFirstVideoFrame = () => {
                if (firstFramePresented) {
                    post("diagnostic", {
                        reason: "first-frame:duplicate",
                        snapshot: collectRenderDiagnostic("first-frame:duplicate")
                    });
                    return;
                }

                firstFramePresented = true;
                post("diagnostic", {
                    reason: "first-frame:begin",
                    snapshot: collectRenderDiagnostic("first-frame:begin")
                });

                if (typeof sdk?.gameVideoPlay === "function") {
                    try {
                        sdk.gameVideoPlay();
                    } catch (error) {
                        post("warning", {
                            message: "gameVideoPlay failed",
                            detail: error?.message || String(error)
                        });
                    }
                }

                if (typeof sdk?.unblockKeyboard === "function") {
                    try {
                        sdk.unblockKeyboard();
                    } catch (error) {
                        post("warning", {
                            message: "unblockKeyboard failed",
                            detail: error?.message || String(error)
                        });
                    }
                }

                if (typeof sdk?.unblockMouse === "function") {
                    try {
                        sdk.unblockMouse();
                    } catch (error) {
                        post("warning", {
                            message: "unblockMouse failed",
                            detail: error?.message || String(error)
                        });
                    }
                }

                // Bitrate/FPS only — do not force resolution renegotiation on first frame.
                applyQualityProfile(payload.bridgeConfig, {
                    reason: "first-frame",
                    resendResolution: false,
                    applyEnhancement: false
                });
                applyMediaAudioState(document);
                hideOverlay();
                focusSurface();
                notifyForeground("first-video-frame");
                post("first-frame");
                post("diagnostic", { reason: "first-frame:end" });
            };

            // Welink 5.15 does not always expose onFirstVideoFrame as a writable
            // method. The media element is the authoritative fallback: once it
            // has decoded data or starts playing, remove the launch overlay and
            // present the stream.
            const attachFirstFrameFallback = (mediaElement) => {
                if (!(mediaElement instanceof HTMLVideoElement) || mediaElement._kuroFirstFrameFallback) {
                    return;
                }

                mediaElement._kuroFirstFrameFallback = true;
                const present = () => {
                    if (mediaElement.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA) {
                        handleFirstVideoFrame();
                    }
                };

                mediaElement.addEventListener("loadeddata", present, { once: true });
                mediaElement.addEventListener("playing", present, { once: true });
                mediaElement.addEventListener("timeupdate", present, { once: true });
                present();
            };

            const setupVideoMouseHandlers = () => {
                const video = document.getElementById("WelinkGameVideo");
                if (!video || video._kuroMouseHandlersSetup) {
                    return;
                }
                video._kuroMouseHandlersSetup = true;
                attachFirstFrameFallback(video);

                video.addEventListener("mousedown", () => {
                    focusSurface();
                }, true);

                video.addEventListener("contextmenu", (e) => {
                    e.preventDefault();
                    return false;
                }, true);
            };

            const waitForVideoAndSetupMouse = () => {
                const existing = document.getElementById("WelinkGameVideo");
                if (existing) {
                    setupVideoMouseHandlers();
                    return;
                }

                const videoObserver = new MutationObserver(() => {
                    const video = document.getElementById("WelinkGameVideo");
                    if (video) {
                        setupVideoMouseHandlers();
                        videoObserver.disconnect();
                    }
                });
                videoObserver.observe(document.documentElement, { childList: true, subtree: true });
            };

            const handlePointerActivation = () => {
                focusSurface();
            };

            window.addEventListener("pointerdown", handlePointerActivation, true);
            window.addEventListener("mousedown", handlePointerActivation, true);
            window.addEventListener("click", handlePointerActivation, true);
            window.addEventListener("focus", () => {
                focusSurface();
                notifyForeground("window-focus");
            }, true);
            window.addEventListener("pageshow", () => {
                focusSurface();
                notifyForeground("pageshow");
            }, true);
            document.addEventListener("visibilitychange", () => {
                if (document.hidden) {
                    notifyBackground("document-hidden");
                    return;
                }

                focusSurface();
                notifyForeground("document-visible");
            }, true);
            window.addEventListener("beforeunload", stopKeepAliveLoop, true);
            window.addEventListener("pagehide", stopKeepAliveLoop, true);

            // WebView2 pointer-lock emits a synthetic recenter move and can
            // also duplicate the same physical delta via pointerrawupdate +
            // mousemove. Filter both so remote camera / aim stay usable.
            const installWebViewPointerFilter = () => {
                if (window.__KURO_POINTER_FILTER_INSTALLED__) {
                    return;
                }

                window.__KURO_POINTER_FILTER_INSTALLED__ = true;

                const originalAddEventListener = EventTarget.prototype.addEventListener;
                const originalRemoveEventListener = EventTarget.prototype.removeEventListener;
                const wrappedListeners = new WeakMap();
                let lastRawMove = null;

                const getWrappedListener = (target, type, listener) => {
                    if (!listener || (type !== "mousemove" && type !== "pointerrawupdate")) {
                        return listener;
                    }

                    let targetListeners = wrappedListeners.get(target);
                    if (!targetListeners) {
                        targetListeners = new Map();
                        wrappedListeners.set(target, targetListeners);
                    }

                    let listenerTypes = targetListeners.get(listener);
                    if (!listenerTypes) {
                        listenerTypes = new Map();
                        targetListeners.set(listener, listenerTypes);
                    }

                    if (listenerTypes.has(type)) {
                        return listenerTypes.get(type);
                    }

                    const wrapped = function (event) {
                        if (document.pointerLockElement) {
                            const movementX = Number(event.movementX) || 0;
                            const movementY = Number(event.movementY) || 0;
                            const now = Number(event.timeStamp) || performance.now();
                            const maxMovementX = Math.max(160, window.innerWidth * 0.2);
                            const maxMovementY = Math.max(120, window.innerHeight * 0.2);

                            if (
                                Math.abs(movementX) > maxMovementX
                                || Math.abs(movementY) > maxMovementY
                            ) {
                                post("pointer-filter", {
                                    reason: "recenter-spike",
                                    movementX,
                                    movementY
                                });
                                return;
                            }

                            if (type === "pointerrawupdate") {
                                lastRawMove = { movementX, movementY, timeStamp: now };
                            } else if (
                                lastRawMove
                                && now - lastRawMove.timeStamp >= 0
                                && now - lastRawMove.timeStamp <= 12
                                && movementX === lastRawMove.movementX
                                && movementY === lastRawMove.movementY
                            ) {
                                return;
                            }
                        }

                        if (typeof listener === "function") {
                            return listener.call(this, event);
                        }

                        return listener.handleEvent(event);
                    };

                    listenerTypes.set(type, wrapped);
                    return wrapped;
                };

                EventTarget.prototype.addEventListener = function (type, listener, options) {
                    return originalAddEventListener.call(
                        this,
                        type,
                        getWrappedListener(this, type, listener),
                        options
                    );
                };

                EventTarget.prototype.removeEventListener = function (type, listener, options) {
                    const wrapped = wrappedListeners
                        .get(this)
                        ?.get(listener)
                        ?.get(type);
                    return originalRemoveEventListener.call(
                        this,
                        type,
                        wrapped || listener,
                        options
                    );
                };
            };

            const loadScript = (source) => new Promise((resolve, reject) => {
                const element = document.createElement("script");
                element.src = source;
                element.async = true;
                element.onload = () => resolve();
                element.onerror = () => reject(new Error({{ToJavaScriptString(LanguageService.GetStringByText("WelinkCloudGame SDK 加载失败。"))}}));
                document.head.appendChild(element);
            });

            const wrapMethod = (target, methodName, beforeInvoke) => {
                if (!target || typeof target[methodName] !== "function") {
                    syncForegroundFlag();
                    return;
                }

                const original = target[methodName].bind(target);
                target[methodName] = (...args) => {
                    try {
                        beforeInvoke(...args);
                    } catch {
                    }

                    return original(...args);
                };
            };

            window.addEventListener("error", (event) => {
                const message = event?.message || {{ToJavaScriptString(LanguageService.GetStringByText("桥页脚本执行失败。"))}};
                post("warning", {
                    message,
                    source: event?.filename || "",
                    line: Number(event?.lineno) || 0,
                    column: Number(event?.colno) || 0
                });
            });

            window.addEventListener("unhandledrejection", (event) => {
                const message = event?.reason?.message || String(event?.reason || {{ToJavaScriptString(LanguageService.GetStringByText("桥页出现未处理异常。"))}});
                post("warning", { message });
            });

            (async () => {
                setMessage({{ToJavaScriptString(LanguageService.GetStringByText("正在加载 WelinkCloudGame SDK……"))}});
                installWebViewPointerFilter();
                await loadScript(payload.scriptUrl);

                if (typeof window.WelinkCloudGame !== "function") {
                    throw new Error({{ToJavaScriptString(LanguageService.GetStringByText("WelinkCloudGame 不可用，无法启动串流。"))}});
                }

                // Same capability gate as the official adapter: H.264/WebRTC is
                // the baseline. WLRTC's reported codec is used only when its
                // asynchronous runtime initialization succeeds as well.
                let useWlRtc = false;
                let selectedCodecType = 18;

                const q = payload.bridgeConfig;
                q.codecType = selectedCodecType;
                const initialResolution = getPhysicalResolution();
                const sdkInitConfig = {
                    ...q,
                    videoWidth: initialResolution.width,
                    videoHeight: initialResolution.height,
                    enableCheckMobileDesktopMode: true,
                    rotate: 0
                };

                sdk = new window.WelinkCloudGame(sdkInitConfig);
                window.__KURO_STREAM_SDK__ = sdk;
                window.WLCG = sdk;

                // Official ordering is important: construct WelinkCloudGame first,
                // then probe and initialize the optional WLRTC runtime.
                try {
                    if (typeof window.WelinkCloudGame.supportWLRTC === "function") {
                        const wlRtcSupport = await window.WelinkCloudGame.supportWLRTC();
                        if (wlRtcSupport && typeof window.WelinkCloudGame.initWLRTC === "function") {
                            // WebView2 exposes chrome.webview. Its WebTransport path
                            // currently reports init success but fails the opening
                            // handshake on first use, then mixes WLRTC/H.265 dispatch
                            // with a Chromium WebRTC/H.264 fallback. Treat that host
                            // runtime as not WLRTC-capable.
                            const wlRtcRuntimeAllowed = !Boolean(window.chrome?.webview);
                            const initialized = wlRtcRuntimeAllowed && await new Promise((resolve) => {
                                let completed = false;
                                const finish = (value) => {
                                    if (completed) return;
                                    completed = true;
                                    resolve(Boolean(value));
                                };
                                try {
                                    window.WelinkCloudGame.initWLRTC(finish);
                                } catch {
                                    finish(false);
                                }
                                setTimeout(() => finish(false), 8000);
                            });
                            // Select WLRTC only when both the SDK initialization and
                            // the current browser host capability gate pass.
                            if (initialized && wlRtcRuntimeAllowed) {
                                const detectedCodec = Number(wlRtcSupport.codec);
                                if (detectedCodec === 18 || detectedCodec === 21) {
                                    selectedCodecType = detectedCodec;
                                    useWlRtc = true;
                                }
                            }
                            post("rtc-capability", {
                                supported: true,
                                initialized,
                                runtimeAllowed: wlRtcRuntimeAllowed,
                                detectedCodec: Number(wlRtcSupport.codec) || 0
                            });
                        }
                    }
                } catch (error) {
                    post("warning", { message: `WLRTC check failed; using WebRTC/H264: ${String(error)}` });
                }

                if (payload.dispatchMessage && typeof payload.dispatchMessage === "object") {
                    payload.dispatchMessage.codecType = selectedCodecType;
                }

                if (typeof sdk.setRtcType === "function" && window.WelinkCloudGame.RTC_TYPE) {
                    const rtcTypes = window.WelinkCloudGame.RTC_TYPE;
                    sdk.setRtcType(useWlRtc ? rtcTypes.WL_RTC : rtcTypes.WEBRTC);
                }
                post("rtc-selection", {
                    transport: useWlRtc ? "WLRTC" : "WEBRTC",
                    codecType: selectedCodecType
                });

                // Welink 5.15 no longer predeclares these callback properties.
                // Assign them unconditionally, as the official adapter does.
                sdk.onGameData = (data) => {
                    handleGameMessage(decodePayload(data));
                };

                sdk.onGameDataWithKey = (key, data) => {
                    handleGameMessageWithKey(key, decodePayload(data));
                };

                sdk.startGameInfo = (code, detail) => {
                    handleSdkMessage(code, detail);
                };

                sdk.startGameError = (code, detail) => {
                    const suffix = detail ? ` | ${typeof detail === "string" ? detail : JSON.stringify(detail)}` : "";
                    post("warning", {
                        message: `Welink startGameError: code=${code}${suffix}`,
                        code,
                        detail
                    });
                };

                if (typeof sdk.onCursorData !== "undefined") {
                    sdk.onCursorData = (visible, url, xHotspot, yHotspot) => {
                        post("cursor-data", {
                            visible: Boolean(visible),
                            hasImage: Boolean(url),
                            xHotspot: Number(xHotspot) || 0,
                            yHotspot: Number(yHotspot) || 0
                        });
                    };
                }

                if (typeof sdk.showGameStatisticsData !== "undefined") {
                    sdk.showGameStatisticsData = (detail) => {
                        lastNetworkStat = detail || null;
                        post("network-stat", { detail });
                    };
                }

                // Auto-reconnect can spawn a second PeerConnection while the first is
                // still decoding; chromium logs showed reconnect + H264 decoder thrash.
                if (typeof sdk.openAutoReconnectServer === "function") {
                    try {
                        sdk.openAutoReconnectServer(true);
                    } catch {
                    }
                }

                if (typeof sdk.setGameScreenAdaptation === "function") {
                    try {
                        sdk.setGameScreenAdaptation(true);
                    } catch {
                    }
                }

                wrapMethod(sdk, "onFirstVideoFrame", () => {
                    handleFirstVideoFrame();
                });

                wrapMethod(sdk, "handleStartGameError", (code, detail, notThrow) => {
                    const suffix = detail ? ` | ${typeof detail === "string" ? detail : JSON.stringify(detail)}` : "";
                    const message = {{ToJavaScriptString(LanguageService.GetStringByText("Welink 协商中出现可恢复告警，代码："))}} + code + suffix;
                    post("warning", { message, code, detail, notThrow: Boolean(notThrow) });
                });

                wrapMethod(sdk, "handlePivotalAction", (name, info) => {
                    post("pivotal", { name, info });
                });

                setMessage({{ToJavaScriptString(LanguageService.GetStringByText("SDK 已加载，正在初始化 Welink 实例……"))}});
                await sdk.init();
                focusSurface();
                syncForegroundFlag();
                notifyForeground("sdk-init");
                waitForVideoAndSetupMouse();

                if (typeof sdk.unblockKeyboard === "function") {
                    try {
                        sdk.unblockKeyboard();
                    } catch {
                    }
                }

                if (typeof sdk.unblockMouse === "function") {
                    try {
                        sdk.unblockMouse();
                    } catch {
                    }
                }

                // Resolution is seeded via videoWidth/videoHeight on the SDK constructor.
                // Calling setGameResolution again here forces an early renegotiation while
                // ICE is still connecting (chromium: repeated H264DecoderImpl create/fail).

                setMessage({{ToJavaScriptString(LanguageService.GetStringByText("Welink 已初始化，正在连接云端实例……"))}});
                await sdk.startGame(payload.dispatchMessage);
                focusSurface();
                syncForegroundFlag();
                notifyForeground("start-game-dispatched");
                startKeepAliveLoop();

                setMessage({{ToJavaScriptString(LanguageService.GetStringByText("串流启动请求已发送，等待首帧到达……"))}});
                post("launch-dispatched");
            })().catch((error) => {
                const message = error?.message || String(error || {{ToJavaScriptString(LanguageService.GetStringByText("串流桥页启动失败。"))}});
                setMessage(message, "error");
                post("error", { message });
            });
        })();
    </script>
</body>
</html>
""";
    }

    public static string BuildTencentBridgeHtml(
        string userKeyJson,
        string deviceIdJson,
        string allocRespJson,
        string tokenJson,
        string bridgeConfigJson)
    {
        return $$$"""
<!doctype html>
<html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<style>
html,body,#kuro-stream-surface{width:100%;height:100%;margin:0;overflow:hidden;background:#000}
#kuro-stream-surface{position:absolute;inset:0}
#kuro-stream-surface canvas,#kuro-stream-surface video{width:100%!important;height:100%!important;object-fit:contain}
#bridge-overlay{position:absolute;inset:0;z-index:20;display:flex;align-items:center;justify-content:center;background:#05080d;color:#eaf4ff;font:22px/1.5 "Segoe UI",sans-serif}
#bridge-overlay.hidden{display:none}
</style></head><body>
<div id="kuro-stream-surface" tabindex="0"></div><div id="bridge-overlay">正在连接腾讯云游戏实例……</div>
<script>
(()=>{
 const nativeRequestPointerLock=Element.prototype.requestPointerLock;
 if(nativeRequestPointerLock){Element.prototype.requestPointerLock=function(){return nativeRequestPointerLock.call(this)}}
 const post=(type,data={})=>{try{chrome.webview.postMessage({type,...data})}catch{}};
 const overlay=document.getElementById('bridge-overlay');
 const surface=document.getElementById('kuro-stream-surface');
 const quality={{{bridgeConfigJson}}};
 const dispatch={userKey:{{{userKeyJson}}},deviceId:{{{deviceIdJson}}},allocRespJson:{{{allocRespJson}}},tk:{{{tokenJson}}}};
 let player=null, firstFrame=false;
 const showError=e=>{overlay.textContent=e?.message||String(e);post('error',{message:overlay.textContent})};
 const present=()=>{if(firstFrame)return;firstFrame=true;overlay.classList.add('hidden');surface.focus();post('first-frame')};
 const snapshot=reason=>post('diagnostic',{reason,visibility:document.visibilityState,focused:document.hasFocus(),inner:[innerWidth,innerHeight],dpr:devicePixelRatio,pointerLock:!!document.pointerLockElement,canvas:[...surface.querySelectorAll('canvas')].map(x=>({id:x.id,w:x.width,h:x.height,cw:x.clientWidth,ch:x.clientHeight,display:getComputedStyle(x).display})),video:[...surface.querySelectorAll('video')].map(x=>({id:x.id,ready:x.readyState,w:x.videoWidth,h:x.videoHeight,cw:x.clientWidth,ch:x.clientHeight,paused:x.paused,display:getComputedStyle(x).display}))});
 const load=src=>new Promise((ok,bad)=>{const s=document.createElement('script');s.src=src;s.onload=ok;s.onerror=()=>bad(new Error('腾讯云游戏 SDK 加载失败'));document.head.appendChild(s)});
 const state=()=>player?.getWebrtcStatus?.() ?? player?.cloudGame?.webrtc?.status;
 const reconnect=async reason=>{try{post('status',{message:'腾讯串流重连：'+reason});await player?.reconnect?.();}catch(e){post('warning',{message:e?.message||String(e)})}};
 const applyQuality=cfg=>{quality.bitRate=cfg?.bitRate??quality.bitRate;quality.fps=cfg?.fps??quality.fps;try{player?.setFrameRate?.(quality.fps||60);player?.setCustomBitrate?.(quality.bitRate||8000)}catch{}return {applied:true}};
 window.__KURO_STREAM_CONTROL__={
   requestExit:()=>player?.quitGame?.(),
   setVolume:v=>player?.setVolume?.(Math.max(0,Math.min(100,Number(v)||0))/100),
   setMuted:m=>player?.setVolume?.(m?0:1),
   applyQualityProfile:applyQuality,
   reconnect,
   notifyForeground:()=>{surface.focus();const s=state();if(s===3||s===4||s==='disconnected'||s==='closed')reconnect('foreground')},
   notifyBackground:()=>{}
 };
 document.addEventListener('visibilitychange',()=>{if(!document.hidden)window.__KURO_STREAM_CONTROL__.notifyForeground()},true);
 document.addEventListener('visibilitychange',()=>snapshot('visibilitychange'),true);
 document.addEventListener('pointerlockchange',()=>snapshot('pointerlockchange'),true);
 window.addEventListener('resize',()=>requestAnimationFrame(()=>snapshot('resize')),true);
 window.addEventListener('online',()=>reconnect('network-online'),true);
 new MutationObserver(()=>{const c=surface.querySelector('canvas');const v=surface.querySelector('video');if(c&&(c.width>0||c.clientWidth>0))present();if(v&&v.readyState>=2)present()}).observe(surface,{childList:true,subtree:true,attributes:true});
 (async()=>{
   const allocation=JSON.parse(dispatch.allocRespJson||'{}');
   if(!allocation.device)throw new Error('腾讯分配数据缺少 device');
   await load('https://newcloudgame.cdn-go.cn/web-sdk/4.9.0/pc.min.js');
   const api=window.CloudGame;
   if(!api?.GameMatrixSDK)throw new Error('GameMatrixSDK 未加载');
   const device=allocation.device;
   player=new api.GameMatrixSDK({
     observable:false,openid:device.identity||'',openkey:dispatch.userKey||'',bussid:device.appid||'',channelid:'',
     gameLandscape:true,clientType:5,gameTag:allocation.realGame||device.tag||'',env:3,resolution:{mode:'full'},
     lockEscKey:false,webrtcTimeout:20000,audio:{enabled:true},enableH265:true,enableDeleteFec:true,
     allocDeviceInfo:{bizInfo:'',type:1,supportInstIp:true,newDevice:false},enableCookies:true,deviceMode:device,
     token:dispatch.tk||'',enableSR:quality.enableImageEnhancement?1:0,needGamepad:true,hideInputUI:true,
     container:'#kuro-stream-surface',host:'https://gamematrix.qq.com'
   });
   window.tencentGamePlayer=player;window.__KURO_STREAM_SDK__=player;
   player.use(new api.PCPlugin({key:[{id:'1',version:'0.1.0',default:true,name:'键盘场景',zoom:{enable:true,p1:{x2:.5,y2:.4},p2:{x2:.5,y2:.6}},nativeEventMode:{enable:true,sensitivity:.65,alwaysSendNativeEvent:true}}],autoUpdateKeyConfigVersion:false}));
   player.registEvent(api.EEvent.VideoReady,present);
   player.registEvent(api.EEvent.GameReady,present);
   player.registEvent(api.EEvent.StateChange,d=>post('diagnostic',{reason:'state-change',state:d}));
   player.registEvent(api.EEvent.WebrtcStat,d=>post('network-stat',{detail:d||{}}));
   player.registEvent(api.EEvent.Reconnect,d=>post('status',{message:'腾讯串流已重连',detail:d}));
   player.registEvent(api.EEvent.Error,e=>post('warning',{message:e?.message||JSON.stringify(e),detail:e}));
   await player.start();
   applyQuality(quality);surface.focus();post('launch-dispatched');
 })().catch(showError);
})();
</script></body></html>
""";
    }

    public static string BuildBootstrapScript(string payloadJson)
    {
        var bootstrapScript = $$"""
(() => {
  const payload = {{payloadJson}};
  const writeStore = (store) => {
    if (!store || !payload.storageItems) {
      return;
    }

    for (const [key, value] of Object.entries(payload.storageItems)) {
      try {
        store.setItem(key, value ?? "");
      } catch {
      }
    }
  };

  try {
    window.__KURO_LAUNCH_OPTIONS__ = payload;
  } catch {
  }

  writeStore(window.localStorage);
  writeStore(window.sessionStorage);

  try {
    window.dispatchEvent(new CustomEvent("kuro-launch-bootstrap", { detail: payload }));
  } catch {
  }
})();
""";

        return bootstrapScript;
    }

    public static string BuildUpdateQalityScript(StreamQualityOptions newQuality)
    {
        var script = $$"""
window.__KURO_STREAM_CONTROL__?.applyQualityProfile?.({
    bitRate: {{newQuality.BitRate}},
    bitRateMin: {{newQuality.BitRateMin}},
    bitRateMax: {{newQuality.BitRateMax}},
    fps: {{newQuality.Fps}},
    targetWidth: {{newQuality.Width}},
    targetHeight: {{newQuality.Height}},
    streamStrategy: "{{newQuality.StreamStrategy}}",
    enableImageEnhancement: {{(newQuality.EnableImageEnhancement ? "true" : "false")}}
});
""";
        return script;
    }
}
