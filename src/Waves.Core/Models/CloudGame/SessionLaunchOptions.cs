using System;
using System.Collections.Generic;
using System.Text;

namespace Waves.Core.Models.CloudGame
{
    public class SessionLaunchOptions
    {/// <summary>
     /// 云游戏主站页面地址。
     /// </summary>
        public required string GameUrl { get; init; }

        /// <summary>
        /// 登录域预热地址，用于先写入 usercenter 会话。
        /// </summary>
        public string BootstrapUrl { get; init; } = string.Empty;

        /// <summary>
        /// 原生业务层已经准备好的串流会话；为空时表示仍需先走官网业务页。
        /// </summary>
        public CloudGameStreamSession? StreamSession { get; init; }

        /// <summary>
        /// 当前会话使用的 access token。
        /// </summary>
        public string AccessToken { get; init; } = string.Empty;

        /// <summary>
        /// 当前会话使用的 refresh 或 phone token。
        /// </summary>
        public string RefreshToken { get; init; } = string.Empty;

        /// <summary>
        /// 写入 WebView2 Cookie 时使用的域名。
        /// </summary>
        public string CookieDomain { get; init; } = string.Empty;

        /// <summary>
        /// 需要按域名注入到请求中的额外请求头。
        /// </summary>
        public IReadOnlyDictionary<string, string> AdditionalHeaders { get; init; } = new Dictionary<string, string>();

        /// <summary>
        /// 启动前需要写入浏览器环境的 Cookie 集合。
        /// </summary>
        public IReadOnlyDictionary<string, string> Cookies { get; init; } = new Dictionary<string, string>();

        /// <summary>
        /// 启动前需要写入 localStorage 或 sessionStorage 的键值集合。
        /// </summary>
        public IReadOnlyDictionary<string, string> StorageItems { get; init; } = new Dictionary<string, string>();

        /// <summary>
        /// 额外请求头允许注入的主机匹配规则。
        /// </summary>
        public IReadOnlyList<string> HeaderHostPatterns { get; init; } = new List<string>();


        /// <summary>
        /// 本次启动使用的串流画质配置。
        /// </summary>
        public StreamQualityOptions Quality { get; init; } = StreamQualityOptions.Default;

        /// <summary>
        /// 当前系统 DPI 值（如 96、120、144、168 等），对齐官方 getDeviceDPI()。
        /// 桥页 Welink SDK 初始化时需要以此为准，而非硬编码 120。
        /// </summary>
        public int StreamDpi { get; init; } = 120;

        /// <summary>
        /// 进入串流后是否隐藏 GameWindow 的顶部与底部浏览器栏，使用纯串流视口展示。
        /// </summary>
        public bool HideBrowserChromeWhenStreaming { get; init; }
    }
}

/// <summary>
/// 描述官方云游戏支持的码率、帧率、分辨率、编解码、网络策略与画质增强参数。
/// </summary>
public sealed record StreamQualityOptions(
    int BitRate,
    int BitRateMin,
    int Fps,
    int Width,
    int Height,
    int CodecType,
    string StreamStrategy,
    bool EnableImageEnhancement,
    string Preset = "clear")  // 原始预设标签，缩放后仍可溯源
{
    /// <summary>
    /// 官方流畅画质标识。
    /// </summary>
    public const string SmoothPreset = "smooth";

    /// <summary>
    /// 官方清晰画质标识。
    /// </summary>
    public const string ClearPreset = "clear";

    /// <summary>
    /// 默认使用的画质配置。
    /// </summary>
    public static readonly StreamQualityOptions Default = FromOfficialPreset(ClearPreset, 60, true);

    /// <summary>
    /// 当前分辨率对应的宽高字符串。
    /// </summary>
    public string ResolutionKey => $"{Width}x{Height}";

    /// <summary>
    /// 当前配置对应的码率范围上限。
    /// </summary>
    public int BitRateMax => BitRate;

    /// <summary>
    /// 按原始预设标签返回对应的官方画质档位（不会因 DPI 缩放而误判）。
    /// </summary>
    public string OfficialPreset => Preset;

    /// <summary>
    /// 当前官方档位对应的中文显示名称。
    /// </summary>
    public string OfficialPresetLabel => string.Equals(OfficialPreset, SmoothPreset, StringComparison.OrdinalIgnoreCase) ? "流畅" : "清晰";

    /// <summary>
    /// 当前画质增强开关对应的中文显示名称。
    /// </summary>
    public string ImageEnhancementLabel => EnableImageEnhancement ? "开启" : "关闭";

    /// <summary>
    /// 根据官方档位与帧率构造桌面端使用的画质配置。
    /// </summary>
    public static StreamQualityOptions FromOfficialPreset(string? preset, int fps, bool enableImageEnhancement = false)
    {
        var normalizedFps = fps == 30 ? 30 : 60;
        var effectivePreset = string.Equals(preset, SmoothPreset, StringComparison.OrdinalIgnoreCase) ? SmoothPreset : ClearPreset;
        return string.Equals(effectivePreset, ClearPreset, StringComparison.OrdinalIgnoreCase)
            ? new StreamQualityOptions(18000, 8000, normalizedFps, 1920, 1080, 21, "0", enableImageEnhancement, ClearPreset)
            : new StreamQualityOptions(5000, 2500, normalizedFps, 1280, 720, 21, "0", enableImageEnhancement, SmoothPreset);
    }
}

public sealed record CloudGameStreamSession
{
    /// <summary>
    /// Welink 分发消息。
    /// </summary>
    public required string DispatchMessage { get; init; }

    /// <summary>
    /// Welink 租户标识。
    /// </summary>
    public required string TenantKey { get; init; }

    /// <summary>
    /// Welink SDK 脚本地址。
    /// </summary>
    public required string ScriptUrl { get; init; }

    /// <summary>
    /// Welink 启动参数。
    /// </summary>
    public required WelinkStartParameters StartParameters { get; init; }

    /// <summary>
    /// 当前会话所在节点区域名称。
    /// </summary>
    public string RegionName { get; init; } = string.Empty;

    /// <summary>
    /// 当前会话键。
    /// </summary>
    public string SessionKey { get; init; } = string.Empty;

    /// <summary>
    /// 钱包时长摘要。
    /// </summary>
    public string WalletSummary { get; init; } = string.Empty;
}
public sealed record WelinkStartParameters(
    string TenantKey,
    string GameId,
    string Resolution,
    int BitRate,
    int Fps,
    int CodecType,
    string Version,
    string CmdLine,
    string BizData);