using System;
using System.Collections.Generic;
using System.Text;

namespace Waves.Core.Contracts.CloudGame;

/// <summary>
/// 云库洛游戏上下文接口
/// </summary>
public interface IKuroCloudGameContext
{
    /// <summary>
    /// 云鸣潮接口
    /// </summary>
    ICloudGameService CloudGameService { get; }

    /// <summary>
    /// 检查本地存储
    /// 1. 清除过期Token
    /// </summary>
    /// <returns></returns>
    Task CheckLocalUserAsync(CancellationToken token);
}
