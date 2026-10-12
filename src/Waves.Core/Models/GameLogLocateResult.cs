namespace Waves.Core.Models;

/// <summary>
/// 鸣潮客户端日志定位结果
/// </summary>
public class GameLogLocateResult
{
    public GameLogLocateState State { get; init; }

    /// <summary>
    /// 定位到的 Client*.log 完整路径（按修改时间倒序），仅 Ready 状态有效
    /// </summary>
    public IReadOnlyList<string> LogFiles { get; init; } = [];
}
