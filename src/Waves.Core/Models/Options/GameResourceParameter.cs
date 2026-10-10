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

    /// <summary>校验时锁定清单的逐包目标版本；主包版本相同但材质包版本不同时用于消除歧义。</summary>
    public IReadOnlyDictionary<string, string>? TargetPackVersions { get; init; }
}
