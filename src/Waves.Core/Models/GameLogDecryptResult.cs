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

    public bool HasFailed => FailedCount > 0;
}
