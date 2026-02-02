using Microsoft.Extensions.Hosting;

namespace Haiyu.BackendService;

public class BackendLifetimeService : IHostedService
{
    private readonly BackendEventSink _eventSink;

    public BackendLifetimeService(BackendEventSink eventSink)
    {
        _eventSink = eventSink;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _eventSink.Enqueue("ServiceStarted");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _eventSink.Enqueue("ServiceStopping");
        return Task.CompletedTask;
    }
}
