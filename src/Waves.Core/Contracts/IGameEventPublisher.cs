using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Models;

namespace Waves.Core.Contracts;

public interface IGameEventPublisher
{
    void Publish(in GameContextOutputArgsV2 @event);
    ValueTask SubscribeAsync(Func<GameContextOutputArgsV2, ValueTask> handler, CancellationToken token);
}