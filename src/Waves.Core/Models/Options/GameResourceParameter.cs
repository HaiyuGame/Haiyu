namespace Waves.Core.Models.Options;

/// <summary>
/// 获取游戏资源时使用的选择参数
/// </summary>
public sealed class GameResourceParameter
{
    /// <summary>
    /// 资源组合名称，例如 HD、SD、UHD。
    /// null 表示使用当前选择或默认组合；旧版单资源包 Provider 忽略此参数。
    /// </summary>
    public string? BundleName { get; init; }

    /// <summary>完整校验索引的明确来源；正式更新不回退到预下载配置。</summary>
    public GameVerificationSource VerificationSource { get; init; } = GameVerificationSource.Official;

    /// <summary>界面资源组合的版本信息，键可能是 HD/SD/UHD；不是已下载清单的逐包校验快照。</summary>
    public IReadOnlyDictionary<string, string>? TargetPackVersions { get; init; }

    /// <summary>仅由安装清单生成的真实分包名与目标版本（含 Common）；修复游戏不设置此约束。</summary>
    public IReadOnlyDictionary<string, string>? VerificationPackVersions { get; init; }
}

/// <summary>校验目标配置来源，与主版本号是否相同无关。</summary>
public enum GameVerificationSource
{
    Official,
    Predownload
}
