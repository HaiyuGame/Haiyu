using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Models;

namespace Waves.Core.Contracts.Events;

/// <summary>
/// 游戏事件发布者接口
/// </summary>
public interface IGameEventPublisher
{
    /// <summary>
    /// 发布事件（非阻塞）
    /// </summary>
    void Publish(in GameContextOutputArgs @event);

    /// <summary>
    /// 订阅事件
    /// </summary>
    ValueTask<IGameEventSubscription> SubscribeAsync(
        Func<GameContextOutputArgs, ValueTask> handler
    );
}
