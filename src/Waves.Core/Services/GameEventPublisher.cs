using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;
using Waves.Core.Contracts;
using Waves.Core.Models;

namespace Waves.Core.Services;

public class GameEventPublisher:IGameEventPublisher
{
    private readonly Channel<GameContextOutputArgsV2> _channel;
    private readonly CancellationTokenSource _cts;
    private readonly List<(Func<GameContextOutputArgsV2, ValueTask> Handler, CancellationToken Token)> _handlers;

    public GameEventPublisher()
    {
        _channel = Channel.CreateBounded<GameContextOutputArgsV2>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _cts = new CancellationTokenSource();
        _handlers = new();

        _ = Task.Run(DispatchEventsAsync);
    }

    public void Publish(in GameContextOutputArgsV2 @event)
    {
        _channel.Writer.TryWrite(@event);
    }

    public ValueTask SubscribeAsync(Func<GameContextOutputArgsV2, ValueTask> handler, CancellationToken token)
    {
        _handlers.Add((handler, token));
        return default;
    }

    private async Task DispatchEventsAsync()
    {
        await foreach (var @event in _channel.Reader.ReadAllAsync(_cts.Token))
        {
            _handlers.RemoveAll(h => h.Token.IsCancellationRequested);

            var tasks = _handlers
                .Select(h => SafelyHandleEvent(h.Handler, @event, h.Token))
                .ToList();

            await Task.WhenAll(tasks);
        }
    }

    private static async Task SafelyHandleEvent(
        Func<GameContextOutputArgsV2, ValueTask> handler,
        GameContextOutputArgsV2 @event,
        CancellationToken token)
    {
        try
        {
            await handler(@event);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
