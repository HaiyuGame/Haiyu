using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;
using Waves.Core.Contracts.Events;
using Waves.Core.Models;

namespace Waves.Core.Services;

public sealed class GameEventPublisher : IGameEventPublisher, IAsyncDisposable
{
    private readonly Channel<GameContextOutputArgs> _channel;
    private readonly CancellationTokenSource _cts;
    private readonly List<SubscriberEntry> _subscribers;
    private readonly Task _dispatchTask;
    private bool _isDisposed;
    private sealed class SubscriberEntry
    {
        public required Guid Id { get; init; }
        public required Func<GameContextOutputArgs, ValueTask> Handler { get; init; }
        public required CancellationTokenSource Cts { get; init; }
        public bool IsDisposed { get; set; }
    }
    public GameEventPublisher()
    {
        _channel = Channel.CreateBounded<GameContextOutputArgs>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest // 缓冲区满时丢弃旧事件
        });
        _cts = new CancellationTokenSource();
        _subscribers = new();
        // 启动后台任务分发事件
        _dispatchTask = Task.Run(DispatchEventsAsync);
    }
    public void Publish(in GameContextOutputArgs @event)
    {
        if (_isDisposed)
            return;
        _channel.Writer.TryWrite(@event);
    }
    public async ValueTask<IGameEventSubscription> SubscribeAsync(
        Func<GameContextOutputArgs, ValueTask> handler)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(GameEventPublisher));
        var id = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        lock (_subscribers)
        {
            _subscribers.Add(new SubscriberEntry
            {
                Id = id,
                Handler = handler,
                Cts = cts
            });
        }
        return new SubscriptionToken(this, id, cts);
    }
    private async Task DispatchEventsAsync()
    {
        try
        {
            await foreach (var @event in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                // 获取活跃订阅者快照
                SubscriberEntry[] subscribersSnapshot;
                lock (_subscribers)
                {
                    subscribersSnapshot = _subscribers
                        .Where(s => !s.IsDisposed)
                        .ToArray();
                }
                // 并行处理所有订阅者
                if (subscribersSnapshot.Length > 0)
                {
                    var tasks = subscribersSnapshot
                        .Select(s => SafelyHandleEvent(s.Handler, @event, s.Cts.Token))
                        .Select(x=>x.AsTask())
                        .ToArray();
                    await Task.WhenAll(tasks);
                }
            }
        }
        catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
        {
            // 正常关闭
        }
    }
    private static async ValueTask SafelyHandleEvent(
        Func<GameContextOutputArgs, ValueTask> handler,
        GameContextOutputArgs @event,
        CancellationToken token)
    {
        try
        {
            await handler(@event);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 订阅者主动取消，正常
        }
        catch (Exception)
        {
            // 订阅者异常，记录但不影响其他订阅者
            // 需要注入 ILogger
        }
    }
    private void Unsubscribe(Guid id)
    {
        lock (_subscribers)
        {
            var subscriber = _subscribers.FirstOrDefault(s => s.Id == id);
            if (subscriber != null)
            {
                subscriber.IsDisposed = true;
                if (!subscriber.Cts.IsCancellationRequested)
                {
                    subscriber.Cts.Cancel();
                    subscriber.Cts.Dispose();
                }
                _subscribers.Remove(subscriber);
            }
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;
        _cts.Cancel();
        await _dispatchTask;
        _cts.Dispose();
        lock (_subscribers)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.Cts.Cancel();
                subscriber.Cts.Dispose();
            }
            _subscribers.Clear();
        }
    }
    private sealed class SubscriptionToken : IGameEventSubscription
    {
        private readonly GameEventPublisher _publisher;
        private readonly Guid _id;
        private readonly CancellationTokenSource _cts;
        private bool _isDisposed;
        public SubscriptionToken(
            GameEventPublisher publisher,
            Guid id,
            CancellationTokenSource cts)
        {
            _publisher = publisher;
            _id = id;
            _cts = cts;
        }
        public bool IsActive => !_isDisposed && !_cts.IsCancellationRequested;
        public void Dispose()
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            _cts.Cancel();
            _cts.Dispose();
            _publisher.Unsubscribe(_id);
        }
    }
}