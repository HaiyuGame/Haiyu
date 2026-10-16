namespace Waves.Core.Models;

/// <summary>
/// 日志解密导出结果汇总
/// </summary>
public class GameLogDecryptResult
{
    /// <summary>
    /// 解密成功（ELog 加密内容）的文件数
    /// </summary>
    public int DecryptedCount { get; init; }

    /// <summary>
    /// 原样复制（已是明文）的文件数
    /// </summary>
    public int CopiedCount { get; init; }

    /// <summary>
    /// 处理失败的文件数（失败详情见应用日志）
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// 解密后校验未通过的文件数（key 不匹配，未导出，详情见应用日志）
    /// </summary>
    public int KeyMismatchCount { get; init; }

    public bool HasFailed => FailedCount > 0;

    /// <summary>
    /// 游戏可能已更换加密 key，解密结果不是有效明文（未导出这些文件）
    /// </summary>
    public bool HasKeyMismatch => KeyMismatchCount > 0;
}
