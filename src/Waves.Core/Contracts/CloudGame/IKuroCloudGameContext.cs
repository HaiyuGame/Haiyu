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
    IWavesCloudGameService CloudGameService { get; }

}
