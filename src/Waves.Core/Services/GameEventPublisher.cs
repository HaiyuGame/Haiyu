namespace Waves.Core.Services;

public sealed class GameEventPublisher
    : EventPublishBase<GameContextOutputArgs>,
        IGameEventPublisher<GameContextOutputArgs>,
        IAsyncDisposable,
        IPublisher
{
    protected override bool IsBarrierEvent(GameContextOutputArgs @event) =>
        @event.Type is GameContextActionType.None or GameContextActionType.GameExit;


    public override async ValueTask<IGameEventSubscription> SubscribeAsync(
        Func<GameContextOutputArgs, ValueTask> handler
    )
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(GameEventPublisher));
        var id = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        lock (_subscribers)
        {
            _subscribers.Add(
                new SubscriberEntry
                {
                    Id = id,
                    Handler = handler,
                    Cts = cts,
                }
            );
        }
        return new SubscriptionToken<GameEventPublisher>(this, id, cts);
    }

    
}

public sealed class SubscriptionToken<Publisher> : IGameEventSubscription
        where Publisher : IPublisher
{
    private readonly Publisher _publisher;
    private readonly Guid _id;
    private readonly CancellationTokenSource _cts;
    private bool _isDisposed;

    public SubscriptionToken(Publisher publisher, Guid id, CancellationTokenSource cts)
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
