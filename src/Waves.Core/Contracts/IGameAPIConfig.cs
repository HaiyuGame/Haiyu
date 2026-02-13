using Waves.Core.Models.Enums;

namespace Waves.Core.Contracts;

/// <summary>
/// 通用游戏API请求配置
/// </summary>
public interface IGameAPIConfig
{
    public CoreType ApiType { get; }
}
