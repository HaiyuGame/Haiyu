namespace Waves.Core.Models.Enums;

/// <summary>
/// 鸣潮客户端日志定位结果状态
/// </summary>
public enum GameLogLocateState
{
    /// <summary>
    /// 游戏目录未设置或不存在
    /// </summary>
    GameFolderMissing,

    /// <summary>
    /// Client\Saved\Logs 目录不存在
    /// </summary>
    LogsFolderMissing,

    /// <summary>
    /// 目录下没有 Client*.log
    /// </summary>
    NoLogFiles,

    /// <summary>
    /// 定位成功
    /// </summary>
    Ready,
}
