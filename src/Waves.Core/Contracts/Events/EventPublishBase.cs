namespace Waves.Core.Contracts.Events
{
    public abstract class EventPublishBase<EventArgs>
    {
        public readonly Channel<EventArgs> Channel;
        public readonly CancellationTokenSource CTS;
        internal readonly List<SubscriberEntry> _subscribers;
        internal readonly Task DispatchTask;
        internal bool _isDisposed;

        public sealed class SubscriberEntry
        {
            public required Guid Id { get; init; }
            public required Func<EventArgs, ValueTask> Handler { get; init; }
            public required CancellationTokenSource Cts { get; init; }
            public bool IsDisposed { get; set; }
        }

        public EventPublishBase()
        {
            Channel = System.Threading.Channels.Channel.CreateBounded<EventArgs>(
                new BoundedChannelOptions(200)
                {
                    FullMode = BoundedChannelFullMode.DropOldest, // 缓冲区满时丢弃旧事件
                }
            );
            CTS = new CancellationTokenSource();
            _subscribers = new();
            DispatchTask = Task.Run(DispatchEventsAsync);
        }

        public void Publish(in EventArgs @event)
        {
            if (_isDisposed)
                return;
            if (@event is not null && IsBarrierEvent(@event))
            {
                while (Channel.Reader.TryRead(out _)) { }
            }

            Channel.Writer.TryWrite(@event);
        }

        protected virtual bool IsBarrierEvent(EventArgs @event) => false;

        public abstract ValueTask<IGameEventSubscription> SubscribeAsync(
            Func<EventArgs, ValueTask> handler
        );


        private async Task DispatchEventsAsync()
        {
            try
            {
                await foreach (var @event in Channel.Reader.ReadAllAsync(CTS.Token))
                {
                    // 获取活跃订阅者快照
                    SubscriberEntry[] subscribersSnapshot;
                    lock (_subscribers)
                    {
                        subscribersSnapshot = _subscribers.Where(s => !s.IsDisposed).ToArray();
                    }
                    // 并行处理所有订阅者
                    if (subscribersSnapshot.Length > 0)
                    {
                        var tasks = subscribersSnapshot
                            .Select(s => SafelyHandleEvent(s.Handler, @event, s.Cts.Token))
                            .Select(x => x.AsTask())
                            .ToArray();
                        await Task.WhenAll(tasks);
                    }
                }
            }
            catch (OperationCanceledException) when (CTS.Token.IsCancellationRequested)
            {
                // 正常关闭
            }
        }

        private static async ValueTask SafelyHandleEvent(
            Func<EventArgs, ValueTask> handler,
            EventArgs @event,
            CancellationToken token
        )
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

        public void Unsubscribe(Guid id)
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
            CTS.Cancel();
            await DispatchTask;
            CTS.Dispose();
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
    }

    public class EventSubscriptionTokenBase<Publisher, EventArgs>
        where Publisher : IPublisher
    {
        private readonly Publisher _publisher;
        private readonly Guid _id;
        private readonly CancellationTokenSource _cts;
        private bool _isDisposed;

        public EventSubscriptionTokenBase(Publisher publisher, Guid id, CancellationTokenSource cts)
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
