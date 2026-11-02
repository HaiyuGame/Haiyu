namespace Waves.Core.Contracts;

/// <summary>
/// 鸣潮客户端日志工具：定位 Client\Saved\Logs 下的 Client*.log 并解密 ELog 加密内容
/// </summary>
public interface IGameLogService
{
    /// <summary>
    /// 定位鸣潮客户端日志文件（Client.log 与 Client-backup-*.log，按修改时间倒序）。
    /// 游戏目录读取最近一次在游戏页选择的鸣潮上下文配置。
    /// </summary>
    /// <param name="token">取消令牌</param>
    /// <returns>定位状态与文件列表</returns>
    Task<GameLogLocateResult?> LocateGameLogsAsync(CancellationToken token = default);

    /// <summary>
    /// 根据游戏核心获取日志记录
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<GameLogLocateResult?> LocateGameLogsAsync(
        IGameContextV2 contextV2,
        CancellationToken token = default
    );

    /// <summary>
    /// 解密单文件
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<string> DecryptGameLogsAsync(string filePath, CancellationToken token = default);

    /// <summary>
    /// 解密日志并导出到输出目录：ELog 加密内容解密，已是明文的文件原样复制；
    /// 输出命名为 原名 + ".decrypted.log"。单个文件失败只记录日志，不中断其余文件。
    /// </summary>
    /// <param name="logFiles">日志文件完整路径列表</param>
    /// <param name="outputFolder">输出目录（已存在）</param>
    /// <param name="token">取消令牌</param>
    /// <returns>解密/复制/失败计数汇总</returns>
    Task<GameLogDecryptResult> DecryptGameLogsAsync(
        IReadOnlyList<string> logFiles,
        string outputFolder,
        CancellationToken token = default
    );
}
